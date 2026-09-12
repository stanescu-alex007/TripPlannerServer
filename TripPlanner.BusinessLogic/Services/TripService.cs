using System.Globalization;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
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
        private readonly TimeProvider _clock;

        public TripService(ITripRepository trips, IUserRepository users, TimeProvider clock)
        {
            _trips = trips;
            _users = users;
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

            var trip = await _trips.CreateAsync(new Trip
            {
                OwnerId = ownerId,
                Name = request.Name.Trim(),
                Location = request.Location.Trim(),
                StartDate = dates.Value.Start,
                EndDate = dates.Value.End,
                Budget = request.Budget,
                TransportType = request.TransportType.ToLowerInvariant(),
                Image = request.Image,
                Description = request.Description
            }, ct);

            // The owner is always the first participant, already confirmed.
            await _trips.AddParticipantAsync(new TripParticipant
            {
                TripId = trip.Id,
                UserId = ownerId,
                Email = owner.Email,
                Nickname = owner.FirstName,
                Role = TripParticipantRole.Owner,
                Status = TripParticipantStatus.Confirmed
            }, ct);

            var invitees = await BuildParticipantsAsync(request.Participants, owner.Email, ct);
            if (invitees.Count > 0)
                await _trips.SyncParticipantsAsync(trip.Id, invitees, ct);

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
            trip.TransportType = request.TransportType.ToLowerInvariant();
            trip.Description = request.Description;
            if (request.Image is not null) trip.Image = request.Image;

            await _trips.UpdateAsync(trip, ct);

            // Only the owner may rewrite the roster; an Admin can edit the trip itself.
            if (request.Participants is not null && TripAccess.IsOwner(trip, requesterId))
            {
                var owner = await _users.GetByIdAsync(trip.OwnerId, ct);
                var desired = await BuildParticipantsAsync(request.Participants, owner?.Email, ct);

                // Sync rather than delete-then-recreate, so anyone who already accepted
                // keeps their Confirmed status instead of being reset to pending.
                await _trips.SyncParticipantsAsync(tripId, desired, ct);
            }

            return Result.Ok();
        }

        public async Task<Result> DeleteTripAsync(Guid tripId, Guid requesterId, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, requesterId)) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.IsOwner(trip, requesterId)) return Result.Fail(ErrorCodes.TripForbidden);

            await _trips.DeleteAsync(tripId, ct);
            return Result.Ok();
        }

        public async Task<Result> AddParticipantAsync(
            Guid tripId, Guid requesterId, AddParticipantRequest request, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, requesterId)) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.IsOwner(trip, requesterId)) return Result.Fail(ErrorCodes.TripForbidden);

            if (!TripParticipantRole.All.Contains(request.Role)
                || string.Equals(request.Role, TripParticipantRole.Owner, StringComparison.OrdinalIgnoreCase))
                return Result.Fail(ErrorCodes.ValidationFailed);

            var email = request.Email.Trim().ToLowerInvariant();

            if (await _trips.GetParticipantByEmailAsync(tripId, email, ct) is not null)
                return Result.Fail(ErrorCodes.ParticipantAlreadyExists);

            var linkedUser = await _users.GetByEmailAsync(email, ct);

            await _trips.AddParticipantAsync(new TripParticipant
            {
                TripId = tripId,
                Email = email,
                UserId = linkedUser?.Id,
                Nickname = request.Nickname.Trim(),
                Role = request.Role,
                Status = TripParticipantStatus.NotConfirmed
            }, ct);

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
                || participant.UserId == trip.OwnerId)
                return Result.Fail(ErrorCodes.OwnerCannotBeRemoved);

            await _trips.RemoveParticipantAsync(participantId, ct);
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
            var result = new List<TripParticipant>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in requested ?? Enumerable.Empty<AddParticipantRequest>())
            {
                if (string.IsNullOrWhiteSpace(p.Email)) continue;

                var email = p.Email.Trim().ToLowerInvariant();

                if (email.Equals(ownerEmail, StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(email)) continue;

                var role = TripParticipantRole.All.Contains(p.Role ?? string.Empty)
                           && !string.Equals(p.Role, TripParticipantRole.Owner, StringComparison.OrdinalIgnoreCase)
                    ? p.Role!
                    : TripParticipantRole.Participant;

                var linkedUser = await _users.GetByEmailAsync(email, ct);

                result.Add(new TripParticipant
                {
                    Email = email,
                    UserId = linkedUser?.Id,
                    Nickname = string.IsNullOrWhiteSpace(p.Nickname) ? email.Split('@')[0] : p.Nickname.Trim(),
                    Role = role,
                    Status = TripParticipantStatus.NotConfirmed
                });
            }

            return result;
        }

        /// <summary>
        /// Parses two "yyyy-MM-dd" strings into UTC midnights. Built with SpecifyKind, not
        /// converted with ToUniversalTime — converting an unspecified date treats it as
        /// local time and lands on the previous day for any positive UTC offset.
        /// </summary>
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

        private TripDto MapToDto(Trip trip, Guid requesterId) => new()
        {
            Id = trip.Id,
            OwnerId = trip.OwnerId,
            Name = trip.Name,
            Location = trip.Location,
            StartDate = trip.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            EndDate = trip.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Budget = trip.Budget,
            TransportType = trip.TransportType,
            Image = trip.Image,
            Description = trip.Description,
            Status = CalculateStatus(trip.StartDate, trip.EndDate),
            CanManage = TripAccess.CanManage(trip, requesterId),
            IsOwner = TripAccess.IsOwner(trip, requesterId),
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
