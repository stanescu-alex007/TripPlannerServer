using System.Globalization;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class TripService : ITripService
    {
        private static readonly IReadOnlySet<string> TransportTypes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "plane", "car", "bus", "train" };

        private readonly ITripRepository _trips;
        private readonly IUserRepository _users;
        private readonly ITripInvitationService _invitations;
        private readonly ITripOwnershipService _ownership;
        private readonly IImageProcessor _imageProcessor;
        private readonly IImageStorage _imageStorage;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public TripService(
            ITripRepository trips,
            IUserRepository users,
            ITripInvitationService invitations,
            ITripOwnershipService ownership,
            IImageProcessor imageProcessor,
            IImageStorage imageStorage,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _trips = trips;
            _users = users;
            _invitations = invitations;
            _ownership = ownership;
            _imageProcessor = imageProcessor;
            _imageStorage = imageStorage;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<List<TripDto>> GetMyTripsAsync(Guid userId, CancellationToken ct = default)
        {
            var trips = await _trips.GetAccessibleByUserAsync(userId, ct);
            return trips.Select(t => MapToDto(t, userId)).ToList();
        }

        public async Task<Result<TripDto>> GetTripByIdAsync(
            Guid tripId, Guid requesterId, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null)
                return Result<TripDto>.Fail(ErrorCodes.TripNotFound);

            // Outsiders get "not found" rather than "forbidden": confirming that a trip
            // exists is itself a leak when the id is all the caller has.
            if (!TripAccess.CanView(trip, requesterId))
                return Result<TripDto>.Fail(ErrorCodes.TripNotFound);

            return Result<TripDto>.Ok(MapToDto(trip, requesterId));
        }

        public async Task<Result<TripDto>> CreateTripAsync(
            Guid ownerId, CreateTripRequest request, CancellationToken ct = default)
        {
            var dates = ParseRange(request.StartDate, request.EndDate);
            if (dates is null) return Result<TripDto>.Fail(ErrorCodes.TripDateRangeInvalid);

            if (!TransportTypes.Contains(request.TransportType))
                return Result<TripDto>.Fail(ErrorCodes.ValidationFailed);

            var owner = await _users.GetByIdAsync(ownerId, ct);
            if (owner is null) return Result<TripDto>.Fail(ErrorCodes.UserNotFound);

            // The trip, its owner row and the roster are one unit. A trip that exists
            // without its owner participant, or with half its invitees, is broken state
            // that no retry repairs.
            var trip = await _unitOfWork.ExecuteAsync(async token =>
            {
                var created = await _trips.CreateAsync(new Trip
                {
                    OwnerId = ownerId,
                    Name = request.Name.Trim(),
                    Location = request.Location.Trim(),
                    StartDate = dates.Value.Start,
                    EndDate = dates.Value.End,
                    Budget = request.Budget,
                    Currency = NormalizeCurrency(request.Currency),
                    TransportType = request.TransportType.ToLowerInvariant(),
                    Description = request.Description
                }, token);

                // The owner is always the first participant, already confirmed.
                await _trips.AddParticipantAsync(new TripParticipant
                {
                    TripId = created.Id,
                    UserId = ownerId,
                    Email = owner.Email,
                    Nickname = owner.FirstName,
                    Role = TripParticipantRole.Owner,
                    Status = TripParticipantStatus.Confirmed
                }, token);

                var invitees = await BuildParticipantsAsync(request.Participants, owner.Email, token);
                if (invitees.Count > 0)
                    await _trips.SyncParticipantsAsync(created.Id, invitees, token);

                return created;
            }, ct);

            // Deliberately outside the transaction: sending mail is slow, fails on its
            // own schedule, and must neither hold a database transaction open nor roll
            // back a trip that was created successfully.
            await _invitations.SendPendingForTripAsync(trip.Id, ct);

            var saved = await _trips.GetByIdWithDetailsAsync(trip.Id, ct);
            return Result<TripDto>.Ok(MapToDto(saved!, ownerId));
        }

        public async Task<Result> UpdateTripAsync(
            Guid tripId, Guid requesterId, UpdateTripRequest request, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, requesterId)) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanManage(trip, requesterId)) return Result.Fail(ErrorCodes.TripForbidden);

            var dates = ParseRange(request.StartDate, request.EndDate);
            if (dates is null) return Result.Fail(ErrorCodes.TripDateRangeInvalid);

            if (!TransportTypes.Contains(request.TransportType))
                return Result.Fail(ErrorCodes.ValidationFailed);

            trip.Name = request.Name.Trim();
            trip.Location = request.Location.Trim();
            trip.StartDate = dates.Value.Start;
            trip.EndDate = dates.Value.End;
            trip.Budget = request.Budget;
            trip.Currency = NormalizeCurrency(request.Currency);
            trip.TransportType = request.TransportType.ToLowerInvariant();
            trip.Description = request.Description;

            var rosterChanged = request.Participants is not null
                                && TripAccess.IsOwner(trip, requesterId);

            await _unitOfWork.ExecuteAsync(async token =>
            {
                await _trips.UpdateAsync(trip, token);

                // Only the owner may rewrite the roster; an Admin can edit the trip itself.
                if (!rosterChanged) return;

                // Only reached by the owner, so OwnerId is set.
                var owner = await _users.GetByIdAsync(trip.OwnerId!.Value, token);
                var desired = await BuildParticipantsAsync(request.Participants!, owner?.Email, token);

                // Sync rather than delete-then-recreate, so anyone who already accepted
                // keeps their Confirmed status instead of being reset to pending.
                await _trips.SyncParticipantsAsync(tripId, desired, token);
            }, ct);

            // Outside the transaction for the same reason as on create. Only people
            // newly added get an email — anyone with a live invitation is skipped.
            if (rosterChanged)
                await _invitations.SendPendingForTripAsync(tripId, ct);

            return Result.Ok();
        }

        public async Task<Result<string>> SetCoverAsync(
            Guid tripId, Guid requesterId, Stream file, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result<string>.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, requesterId)) return Result<string>.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanManage(trip, requesterId)) return Result<string>.Fail(ErrorCodes.TripForbidden);

            var processed = await _imageProcessor.ProcessAsync(file, ImageKind.TripCover, ct);
            if (!processed.Success) return Result<string>.Fail(processed.Error!);

            var previous = trip.Image;
            var url = await _imageStorage.SaveAsync(ImageFolders.TripCovers, processed.Data!, ct);

            trip.Image = url;

            try
            {
                await _trips.UpdateAsync(trip, ct);
            }
            catch
            {
                // Nothing points at the new file, so it must not linger.
                await _imageStorage.DeleteAsync(url, CancellationToken.None);
                throw;
            }

            await _imageStorage.DeleteAsync(previous, ct);

            return Result<string>.Ok(url);
        }

        public async Task<Result> RemoveCoverAsync(Guid tripId, Guid requesterId, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, requesterId)) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanManage(trip, requesterId)) return Result.Fail(ErrorCodes.TripForbidden);

            var previous = trip.Image;
            if (previous is null) return Result.Ok();

            trip.Image = null;
            await _trips.UpdateAsync(trip, ct);
            await _imageStorage.DeleteAsync(previous, ct);

            return Result.Ok();
        }

        public async Task<Result> DeleteTripAsync(Guid tripId, Guid requesterId, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, requesterId)) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.IsOwner(trip, requesterId)) return Result.Fail(ErrorCodes.TripForbidden);

            // Deleted for everyone on it at once, so it goes to the deleter's own list
            // and can be brought back from there.
            await _trips.DeleteAsync(tripId, requesterId, ct);
            return Result.Ok();
        }

        public async Task<Result> AddParticipantAsync(
            Guid tripId, Guid requesterId, AddParticipantRequest request, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, requesterId)) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.IsOwner(trip, requesterId)) return Result.Fail(ErrorCodes.TripForbidden);

            var role = TripParticipantRole.Canonicalize(request.Role);

            if (role is null || role == TripParticipantRole.Owner)
                return Result.Fail(ErrorCodes.ValidationFailed);

            var email = EmailAddress.Normalize(request.Email);

            if (await _trips.GetParticipantByEmailAsync(tripId, email, ct) is not null)
                return Result.Fail(ErrorCodes.ParticipantAlreadyExists);

            var linkedUser = await _users.GetByEmailAsync(email, ct);

            var participant = new TripParticipant
            {
                TripId = tripId,
                Email = email,
                UserId = linkedUser?.Id,
                Nickname = request.Nickname.Trim(),
                Role = role,
                Status = TripParticipantStatus.NotConfirmed
            };

            try
            {
                await _trips.AddParticipantAsync(participant, ct);
            }
            catch (DuplicateParticipantException)
            {
                // Added by a parallel request between the check above and this insert;
                // the unique index on (TripId, Email) decided.
                return Result.Fail(ErrorCodes.ParticipantAlreadyExists);
            }

            await _invitations.SendAsync(participant.Id, ct);

            return Result.Ok();
        }

        public async Task<Result> RemoveParticipantAsync(
            Guid tripId, Guid requesterId, Guid participantId, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, requesterId)) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.IsOwner(trip, requesterId)) return Result.Fail(ErrorCodes.TripForbidden);

            var participant = trip.Participants.FirstOrDefault(p => p.Id == participantId);
            if (participant is null) return Result.Fail(ErrorCodes.ParticipantNotFound);

            // Check the role, not just UserId — an owner row whose UserId happens to be
            // null would otherwise slip through.
            if (string.Equals(participant.Role, TripParticipantRole.Owner, StringComparison.OrdinalIgnoreCase)
                || (trip.OwnerId is not null && participant.UserId == trip.OwnerId))
                return Result.Fail(ErrorCodes.OwnerCannotBeRemoved);

            await _trips.RemoveParticipantAsync(participantId, ct);
            return Result.Ok();
        }

        public async Task<Result> LeaveTripAsync(Guid tripId, Guid userId, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, userId)) return Result.Fail(ErrorCodes.TripNotFound);

            var seat = trip.Participants.FirstOrDefault(p => p.UserId == userId);
            if (seat is null) return Result.Fail(ErrorCodes.ParticipantNotFound);

            if (TripAccess.IsOwner(trip, userId)
                || string.Equals(seat.Role, TripParticipantRole.Owner, StringComparison.OrdinalIgnoreCase))
                return Result.Fail(ErrorCodes.OwnerCannotLeave);

            OwnershipOfferNotice? notice;

            try
            {
                notice = await _unitOfWork.ExecuteAsync(async token =>
                {
                    // If the trip was being offered to them, it has to move on while their
                    // seat is still there to be ruled out.
                    var next = await _ownership.WithdrawOfferAsync(tripId, userId, token);

                    await _trips.RemoveParticipantAsync(seat.Id, token);
                    return next;
                }, ct);
            }
            catch (ConcurrencyConflictException)
            {
                // The offer was answered or lapsed at the same instant. The transaction
                // rolled back, so they are still on the trip; trying again sees the
                // offer's new state and goes through.
                return Result.Fail(ErrorCodes.ConcurrencyConflict);
            }

            if (notice is not null) await _ownership.NotifyCandidatesAsync([notice], ct);

            return Result.Ok();
        }

        public async Task<List<DeletedTripDto>> GetDeletedTripsAsync(
            Guid userId, CancellationToken ct = default)
        {
            var since = _clock.GetUtcNow().UtcDateTime - TripRetention.DeletedTripWindow;

            var trips = await _trips.GetDeletedByUserAsync(userId, since, ct);

            return trips.Select(t => new DeletedTripDto
            {
                Id = t.Id,
                Name = t.Name,
                Location = t.Location,
                StartDate = t.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndDate = t.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DeletedAt = t.DeletedAt!.Value,
                PurgeAt = t.DeletedAt!.Value + TripRetention.DeletedTripWindow
            }).ToList();
        }

        public async Task<Result> RestoreTripAsync(
            Guid tripId, Guid userId, CancellationToken ct = default)
        {
            var trip = await _trips.GetDeletedByIdAsync(tripId, ct);

            // Only the person who deleted it, and only while it is still there. Anyone
            // else is told it does not exist, which from their side is true.
            if (trip?.DeletedAt is null || trip.DeletedByUserId != userId)
                return Result.Fail(ErrorCodes.TripNotFound);

            if (trip.DeletedAt < _clock.GetUtcNow().UtcDateTime - TripRetention.DeletedTripWindow)
                return Result.Fail(ErrorCodes.TripNotFound);

            await _unitOfWork.ExecuteAsync(token => _trips.RestoreAsync(trip, token), ct);

            return Result.Ok();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Turns the requested roster into entities, dropping blanks, the owner, and
        /// duplicate addresses. Looks each email up once so a repeated address does not
        /// re-query.
        /// </summary>
        private async Task<List<TripParticipant>> BuildParticipantsAsync(
            IEnumerable<AddParticipantRequest>? requested, string? ownerEmail, CancellationToken ct)
        {
            var wanted = new List<(string Email, AddParticipantRequest Request)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in requested ?? Enumerable.Empty<AddParticipantRequest>())
            {
                if (string.IsNullOrWhiteSpace(p.Email)) continue;

                var email = EmailAddress.Normalize(p.Email);

                if (email.Equals(ownerEmail, StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(email)) continue;

                wanted.Add((email, p));
            }

            if (wanted.Count == 0) return [];

            // One query for the whole roster rather than one per invitee: a trip with
            // twenty people used to mean twenty round trips to the database.
            var existing = await _users.GetIdsByEmailsAsync(wanted.Select(w => w.Email).ToList(), ct);

            return wanted.Select(w =>
            {
                // Canonical spelling, never the raw value: the CHECK constraint is
                // case-sensitive. Unknown roles and "Owner" fall back to Participant.
                var requestedRole = TripParticipantRole.Canonicalize(w.Request.Role);

                var role = requestedRole is not null && requestedRole != TripParticipantRole.Owner
                    ? requestedRole
                    : TripParticipantRole.Participant;

                return new TripParticipant
                {
                    Email = w.Email,
                    UserId = existing.TryGetValue(w.Email, out var id) ? id : null,
                    Nickname = string.IsNullOrWhiteSpace(w.Request.Nickname)
                        ? w.Email.Split('@')[0]
                        : w.Request.Nickname.Trim(),
                    Role = role,
                    Status = TripParticipantStatus.NotConfirmed
                };
            }).ToList();
        }

        /// <summary>
        /// Parses two "yyyy-MM-dd" strings into UTC midnights. Built with SpecifyKind, not
        /// converted with ToUniversalTime — converting an unspecified date treats it as
        /// local time and lands on the previous day for any positive UTC offset.
        /// </summary>
        /// <summary>Falls back to EUR rather than rejecting: the field is optional.</summary>
        private static string NormalizeCurrency(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "EUR" : value.Trim().ToUpperInvariant();

        private static (DateTime Start, DateTime End)? ParseRange(string start, string end)
        {
            if (!DateOnly.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var from)) return null;

            if (!DateOnly.TryParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var to)) return null;

            if (to < from) return null;

            return (
                DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                DateTime.SpecifyKind(to.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
        }

        private TripDto MapToDto(Trip trip, Guid requesterId)
        {
            var offer = trip.IsAwaitingOwner
                ? trip.OwnershipTransfers?.FirstOrDefault(o => o.Status == TripOwnershipTransferStatus.Pending)
                : null;

            var candidate = offer is null
                ? null
                : trip.Participants?.FirstOrDefault(p => p.Id == offer.CandidateParticipantId);

            return new TripDto
            {
                Id = trip.Id,
                OwnerId = trip.OwnerId,
                Name = trip.Name,
                Location = trip.Location,
                StartDate = trip.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndDate = trip.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Budget = trip.Budget,
                Currency = trip.Currency,
                TransportType = trip.TransportType,
                Image = trip.Image,
                Description = trip.Description,
                Status = CalculateStatus(trip.StartDate, trip.EndDate),
                CanManage = TripAccess.CanManage(trip, requesterId),
                IsOwner = TripAccess.IsOwner(trip, requesterId),
                AwaitingOwner = trip.IsAwaitingOwner,
                OwnershipTransfer = offer is null ? null : new OwnershipTransferDto
                {
                    Id = offer.Id,
                    CandidateNickname = candidate?.Nickname ?? string.Empty,
                    ExpiresAt = offer.ExpiresAt,
                    IsForYou = candidate?.UserId is not null && candidate.UserId == requesterId
                },
                Participants = (trip.Participants ?? Enumerable.Empty<TripParticipant>())
                    .OrderByDescending(p => p.Role == TripParticipantRole.Owner)
                    .ThenBy(p => p.Nickname)
                    .Select(p => new TripParticipantDto
                    {
                        Id = p.Id,
                        UserId = p.UserId,
                        Email = p.Email,
                        Nickname = p.Nickname,
                        Role = p.Role,
                        Status = p.Status
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// EndDate is the last day <em>inclusive</em>, so the comparison runs against the
        /// end of that day. Comparing against its midnight marked a trip completed from
        /// the first moment of its final day.
        /// </summary>
        private string CalculateStatus(DateTime startDate, DateTime endDate)
        {
            var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
            var start = DateOnly.FromDateTime(startDate);
            var end = DateOnly.FromDateTime(endDate);

            if (today < start) return "upcoming";
            if (today > end) return "completed";
            return "active";
        }
    }
}
