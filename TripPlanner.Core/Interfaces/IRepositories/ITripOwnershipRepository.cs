using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    /// <summary>
    /// Data access for handing a trip over. Entities come back tracked, together with
    /// the trip's live participants, and <see cref="SaveAsync"/> persists whatever the
    /// service changed on them — ownership moves several rows at once.
    /// </summary>
    public interface ITripOwnershipRepository
    {
        Task<List<Trip>> GetOwnedTripsAsync(Guid ownerId, CancellationToken ct = default);

        /// <summary>Any status, so the caller can explain why an offer can no longer be answered.</summary>
        Task<TripOwnershipTransfer?> GetByIdAsync(Guid transferId, CancellationToken ct = default);

        /// <summary>Pending offers addressed to the user, lapsed ones included.</summary>
        Task<List<TripOwnershipTransfer>> GetPendingForUserAsync(Guid userId, CancellationToken ct = default);

        Task<List<Guid>> GetOverdueIdsAsync(DateTime now, CancellationToken ct = default);

        /// <summary>
        /// Seats already asked since the trip last changed hands — declined, lapsed or
        /// withdrawn. They are skipped when choosing who to ask next.
        /// </summary>
        Task<HashSet<Guid>> GetPassedOverParticipantIdsAsync(Guid tripId, CancellationToken ct = default);

        Task AddAsync(TripOwnershipTransfer transfer, CancellationToken ct = default);

        /// <summary>
        /// Throws <see cref="Common.ConcurrencyConflictException"/> when an offer was
        /// answered or lapsed by another request in the meantime.
        /// </summary>
        Task SaveAsync(CancellationToken ct = default);
    }
}
