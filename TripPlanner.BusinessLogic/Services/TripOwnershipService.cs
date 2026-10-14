using System.Globalization;
using Microsoft.Extensions.Logging;
using TripPlanner.BusinessLogic.Email;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class TripOwnershipService : ITripOwnershipService
    {
        /// <summary>Long enough to reach someone back from a week away.</summary>
        private const int OfferValidDays = 7;

        private readonly ITripOwnershipRepository _ownership;
        private readonly ITripRepository _trips;
        private readonly ITripInvitationRepository _invitations;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _email;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<TripOwnershipService> _logger;

        public TripOwnershipService(
            ITripOwnershipRepository ownership,
            ITripRepository trips,
            ITripInvitationRepository invitations,
            IUnitOfWork unitOfWork,
            IEmailSender email,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<TripOwnershipService> logger)
        {
            _ownership = ownership;
            _trips = trips;
            _invitations = invitations;
            _unitOfWork = unitOfWork;
            _email = email;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // ── ACCOUNT DELETION ──────────────────────────────────────────────────

        public async Task<IReadOnlyList<OwnershipOfferNotice>> ReleaseDepartingUserAsync(
            Guid userId, CancellationToken ct = default)
        {
            var owned = await _ownership.GetOwnedTripsAsync(userId, ct);
            foreach (var trip in owned) trip.OwnerId = null;

            // Offers that were waiting on this user can no longer be answered.
            var waitingOnUser = await _ownership.GetPendingForUserAsync(userId, ct);
            foreach (var offer in waitingOnUser) offer.Status = TripOwnershipTransferStatus.Withdrawn;

            await _ownership.SaveAsync(ct);

            var notices = new List<OwnershipOfferNotice>();

            foreach (var trip in owned.Concat(waitingOnUser.Select(o => o.Trip)))
            {
                // The user's seats are only vacated after this runs, so they have to be
                // ruled out explicitly or a trip could be offered straight back to them.
                var notice = await OfferToNextAsync(trip, departingUserId: userId, ct);
                if (notice is not null) notices.Add(notice);
            }

            return notices;
        }

        public async Task<OwnershipOfferNotice?> WithdrawOfferAsync(
            Guid tripId, Guid userId, CancellationToken ct = default)
        {
            var offers = await _ownership.GetPendingForUserAsync(userId, ct);
            var offer = offers.FirstOrDefault(o => o.TripId == tripId);

            if (offer is null) return null;

            offer.Status = TripOwnershipTransferStatus.Withdrawn;
            await _ownership.SaveAsync(ct);

            // The seat is still there at this point, so it has to be ruled out explicitly.
            return await OfferToNextAsync(offer.Trip, departingUserId: userId, ct);
        }

        public async Task NotifyCandidatesAsync(
            IEnumerable<OwnershipOfferNotice> offers, CancellationToken ct = default)
        {
            foreach (var offer in offers)
            {
                await _email.TrySendAsync(
                    AuthEmailTemplates.OwnershipOffered(
                        toAddress: offer.CandidateEmail,
                        candidateName: offer.CandidateNickname,
                        tripName: offer.TripName,
                        link: FrontendLinks.TripDetail(_settings, offer.TripId),
                        validDays: OfferValidDays),
                    _logger, ct);
            }
        }

        // ── RESPONDING ────────────────────────────────────────────────────────

        public async Task<List<OwnershipOfferDto>> GetOffersForUserAsync(
            Guid userId, CancellationToken ct = default)
        {
            var now = Now;
            var offers = await _ownership.GetPendingForUserAsync(userId, ct);

            // A lapsed offer the sweep has not reached yet is no longer answerable.
            return offers
                .Where(o => o.ExpiresAt > now)
                .Select(o => new OwnershipOfferDto
                {
                    Id = o.Id,
                    TripId = o.TripId,
                    TripName = o.Trip.Name,
                    Location = o.Trip.Location,
                    StartDate = FormatDate(o.Trip.StartDate),
                    EndDate = FormatDate(o.Trip.EndDate),
                    ExpiresAt = o.ExpiresAt
                })
                .ToList();
        }

        public async Task<Result> AcceptAsync(Guid transferId, Guid userId, CancellationToken ct = default)
        {
            var offer = await _ownership.GetByIdAsync(transferId, ct);

            // Someone else's offer is reported as missing, not as forbidden.
            if (offer is null || offer.Candidate.UserId != userId)
                return Result.Fail(ErrorCodes.OwnershipOfferNotFound);

            var closed = await EnsureOpenAsync(offer, ct);
            if (closed is not null) return Result.Fail(closed);

            try
            {
                await _unitOfWork.ExecuteAsync(async t =>
                {
                    offer.Status = TripOwnershipTransferStatus.Accepted;
                    offer.RespondedAt = Now;

                    offer.Trip.OwnerId = userId;
                    offer.Candidate.Role = TripParticipantRole.Owner;

                    await _ownership.SaveAsync(t);
                }, ct);
            }
            catch (ConcurrencyConflictException)
            {
                // Lapsed by the sweep at the same instant; its outcome stands.
                return Result.Fail(ErrorCodes.OwnershipOfferAlreadyAnswered);
            }

            _logger.LogInformation("User {UserId} took over trip {TripId}.", userId, offer.TripId);
            return Result.Ok();
        }

        public async Task<Result> DeclineAsync(Guid transferId, Guid userId, CancellationToken ct = default)
        {
            var offer = await _ownership.GetByIdAsync(transferId, ct);

            if (offer is null || offer.Candidate.UserId != userId)
                return Result.Fail(ErrorCodes.OwnershipOfferNotFound);

            var closed = await EnsureOpenAsync(offer, ct);
            if (closed is not null) return Result.Fail(closed);

            OwnershipOfferNotice? next;

            try
            {
                next = await _unitOfWork.ExecuteAsync(async t =>
                {
                    offer.Status = TripOwnershipTransferStatus.Declined;
                    offer.RespondedAt = Now;
                    await _ownership.SaveAsync(t);

                    return await OfferToNextAsync(offer.Trip, departingUserId: null, t);
                }, ct);
            }
            catch (ConcurrencyConflictException)
            {
                return Result.Fail(ErrorCodes.OwnershipOfferAlreadyAnswered);
            }

            // Mail only once the hand-on has committed.
            if (next is not null) await NotifyCandidatesAsync([next], ct);

            _logger.LogInformation("User {UserId} declined ownership of trip {TripId}.", userId, offer.TripId);
            return Result.Ok();
        }

        // ── EXPIRY ────────────────────────────────────────────────────────────

        public async Task<int> ExpireOverdueOffersAsync(CancellationToken ct = default)
        {
            var lapsed = 0;

            foreach (var id in await _ownership.GetOverdueIdsAsync(Now, ct))
            {
                // Loaded one at a time: a conflict on one offer clears the change tracker,
                // which would leave any offers loaded alongside it silently detached.
                var offer = await _ownership.GetByIdAsync(id, ct);

                if (offer is null
                    || offer.Status != TripOwnershipTransferStatus.Pending
                    || offer.ExpiresAt > Now)
                    continue;

                if (await LapseAsync(offer, ct)) lapsed++;
            }

            return lapsed;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Null when the offer can still be answered. An offer past its expiry that the
        /// sweep has not reached yet is lapsed on the spot, so it moves on as soon as
        /// anyone touches it rather than whenever the job next runs.
        /// </summary>
        private async Task<Error?> EnsureOpenAsync(TripOwnershipTransfer offer, CancellationToken ct)
        {
            if (offer.Status == TripOwnershipTransferStatus.Pending)
            {
                if (offer.ExpiresAt > Now) return null;

                await LapseAsync(offer, ct);
                return ErrorCodes.OwnershipOfferExpired;
            }

            return offer.Status == TripOwnershipTransferStatus.Expired
                ? ErrorCodes.OwnershipOfferExpired
                : ErrorCodes.OwnershipOfferAlreadyAnswered;
        }

        /// <summary>False when the offer was answered at the same moment and that answer won.</summary>
        private async Task<bool> LapseAsync(TripOwnershipTransfer offer, CancellationToken ct)
        {
            OwnershipOfferNotice? next;

            try
            {
                next = await _unitOfWork.ExecuteAsync(async t =>
                {
                    offer.Status = TripOwnershipTransferStatus.Expired;
                    await _ownership.SaveAsync(t);

                    return await OfferToNextAsync(offer.Trip, departingUserId: null, t);
                }, ct);
            }
            catch (ConcurrencyConflictException)
            {
                return false;
            }

            if (next is not null) await NotifyCandidatesAsync([next], ct);

            _logger.LogInformation("Ownership offer {OfferId} for trip {TripId} lapsed.", offer.Id, offer.TripId);
            return true;
        }

        /// <summary>
        /// Offers the trip to the most senior participant not yet asked, or archives it
        /// when there is nobody left.
        /// </summary>
        private async Task<OwnershipOfferNotice?> OfferToNextAsync(
            Trip trip, Guid? departingUserId, CancellationToken ct)
        {
            var passedOver = await _ownership.GetPassedOverParticipantIdsAsync(trip.Id, ct);

            // Only people who can actually run the trip: they have an account and have
            // said yes to being on it. Someone who never answered their invitation is
            // not a sensible heir.
            var next = trip.Participants
                .Where(p => p.UserId is not null
                            && p.UserId != departingUserId
                            && p.DeletedAt is null
                            && string.Equals(p.Status, TripParticipantStatus.Confirmed, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(p.Role, TripParticipantRole.Owner, StringComparison.OrdinalIgnoreCase)
                            && !passedOver.Contains(p.Id))
                .OrderBy(p => Seniority(p.Role))
                .ThenBy(p => p.CreatedAt)
                .FirstOrDefault();

            if (next is null)
            {
                // Nobody can join a trip that nobody runs.
                await _invitations.InvalidateOpenForTripAsync(trip.Id, ct);

                // Deleted with no owner to put it in a deleted list: there is nobody left
                // who could restore it, so it just waits out the retention window.
                await _trips.DeleteAsync(trip.Id, deletedByUserId: null, ct);

                _logger.LogInformation("Trip {TripId} deleted: nobody left to take it over.", trip.Id);
                return null;
            }

            var offer = new TripOwnershipTransfer
            {
                TripId = trip.Id,
                CandidateParticipantId = next.Id,
                ExpiresAt = Now.AddDays(OfferValidDays)
            };

            await _ownership.AddAsync(offer, ct);

            _logger.LogInformation(
                "Trip {TripId} offered to participant {ParticipantId}.", trip.Id, next.Id);

            return new OwnershipOfferNotice(
                offer.Id, trip.Id, trip.Name, next.Email, next.Nickname, offer.ExpiresAt);
        }

        /// <summary>Lower goes first: whoever already helps run the trip is asked before a guest.</summary>
        private static int Seniority(string role) => role switch
        {
            _ when string.Equals(role, TripParticipantRole.Admin, StringComparison.OrdinalIgnoreCase) => 0,
            _ when string.Equals(role, TripParticipantRole.Vip, StringComparison.OrdinalIgnoreCase) => 1,
            _ => 2
        };

        private static string FormatDate(DateTime value) =>
            value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
