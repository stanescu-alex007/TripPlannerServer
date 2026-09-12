using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface ITripRepository
    {
        /// <summary>
        /// Every trip the user can see — owned <em>and</em> ones they were invited to.
        /// The old query filtered on OwnerId only, so trips you were a participant on
        /// never appeared anywhere in the app.
        /// </summary>
        Task<List<Trip>> GetAccessibleByUserAsync(Guid userId, CancellationToken ct = default);

        Task<Trip?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>Includes participants — needed by every access check.</summary>
        Task<Trip?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

        Task<Trip> CreateAsync(Trip trip, CancellationToken ct = default);

        Task UpdateAsync(Trip trip, CancellationToken ct = default);

        /// <summary>Soft-deletes the trip together with its participants and schedule.</summary>
        Task DeleteAsync(Guid tripId, CancellationToken ct = default);

        Task AddParticipantAsync(TripParticipant participant, CancellationToken ct = default);

        Task UpdateParticipantAsync(TripParticipant participant, CancellationToken ct = default);

        Task RemoveParticipantAsync(Guid participantId, CancellationToken ct = default);

        /// <summary>Expects an already-normalized address.</summary>
        Task<TripParticipant?> GetParticipantByEmailAsync(Guid tripId, string email, CancellationToken ct = default);

        /// <summary>
        /// Replaces the non-owner roster in one transaction, preserving the Status of
        /// participants that survive the edit.
        /// </summary>
        Task SyncParticipantsAsync(Guid tripId, IReadOnlyCollection<TripParticipant> desired, CancellationToken ct = default);
    }
}
