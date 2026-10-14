using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// What happens to a trip when its owner deletes their account: it is offered to the
    /// next person in line — Admin, then VIP, then Participant, earliest added first —
    /// who can accept or decline. When nobody is left to ask, the trip is deleted.
    /// </summary>
    public interface ITripOwnershipService
    {
        /// <summary>
        /// Runs inside the account-deletion transaction. Releases every trip the user
        /// owns and passes on any offer that was waiting on them. Returns the offers
        /// created, to be announced with <see cref="NotifyCandidatesAsync"/> once the
        /// deletion has committed.
        /// </summary>
        Task<IReadOnlyList<OwnershipOfferNotice>> ReleaseDepartingUserAsync(
            Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Withdraws the offer waiting on someone who is leaving the trip, and passes it
        /// to the next person. Runs inside the caller's transaction.
        /// </summary>
        Task<OwnershipOfferNotice?> WithdrawOfferAsync(
            Guid tripId, Guid userId, CancellationToken ct = default);

        Task NotifyCandidatesAsync(IEnumerable<OwnershipOfferNotice> offers, CancellationToken ct = default);

        /// <summary>Open offers addressed to the user.</summary>
        Task<List<OwnershipOfferDto>> GetOffersForUserAsync(Guid userId, CancellationToken ct = default);

        Task<Result> AcceptAsync(Guid transferId, Guid userId, CancellationToken ct = default);

        Task<Result> DeclineAsync(Guid transferId, Guid userId, CancellationToken ct = default);

        /// <summary>Passes on offers nobody answered in time. Returns how many lapsed.</summary>
        Task<int> ExpireOverdueOffersAsync(CancellationToken ct = default);
    }
}
