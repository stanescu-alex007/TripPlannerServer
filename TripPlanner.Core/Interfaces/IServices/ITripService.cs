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

        Task<Result> DeleteTripAsync(Guid tripId, Guid requesterId, CancellationToken ct = default);

        Task<Result> AddParticipantAsync(Guid tripId, Guid requesterId, AddParticipantRequest request, CancellationToken ct = default);

        Task<Result> RemoveParticipantAsync(Guid tripId, Guid requesterId, Guid participantId, CancellationToken ct = default);
    }
}
