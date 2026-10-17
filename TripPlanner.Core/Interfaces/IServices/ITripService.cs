using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface ITripService
    {
        /// <summary>Trips the user owns plus those they were invited to.</summary>
        Task<List<TripDto>> GetMyTripsAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Takes the requester so access can be checked — the previous signature had no
        /// caller at all, which is what made any trip readable by id.
        /// </summary>
        Task<Result<TripDto>> GetTripByIdAsync(Guid tripId, Guid requesterId, CancellationToken ct = default);

        Task<Result<TripDto>> CreateTripAsync(Guid ownerId, CreateTripRequest request, CancellationToken ct = default);

        Task<Result> UpdateTripAsync(Guid tripId, Guid requesterId, UpdateTripRequest request, CancellationToken ct = default);

        /// <summary>
        /// Processes and stores a new cover, replacing (and deleting) the old one. Same
        /// rule as editing the trip: owner or a confirmed Admin. Returns the new URL.
        /// </summary>
        Task<Result<string>> SetCoverAsync(Guid tripId, Guid requesterId, Stream file, CancellationToken ct = default);

        Task<Result> RemoveCoverAsync(Guid tripId, Guid requesterId, CancellationToken ct = default);

        Task<Result> DeleteTripAsync(Guid tripId, Guid requesterId, CancellationToken ct = default);

        Task<Result> AddParticipantAsync(Guid tripId, Guid requesterId, AddParticipantRequest request, CancellationToken ct = default);

        Task<Result> RemoveParticipantAsync(Guid tripId, Guid requesterId, Guid participantId, CancellationToken ct = default);

        /// <summary>
        /// The caller takes themselves off the trip. Open to anyone on it except the
        /// owner, who has nobody to hand it to.
        /// </summary>
        Task<Result> LeaveTripAsync(Guid tripId, Guid userId, CancellationToken ct = default);

        /// <summary>Trips the caller deleted, still inside the restore window.</summary>
        Task<List<DeletedTripDto>> GetDeletedTripsAsync(Guid userId, CancellationToken ct = default);

        Task<Result> RestoreTripAsync(Guid tripId, Guid userId, CancellationToken ct = default);
    }
}
