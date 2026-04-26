using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface ITripService
    {
        Task<List<TripDto>> GetMyTripsAsync(Guid userId);
        Task<TripDto?> GetTripByIdAsync(Guid tripId);
        Task<TripDto> CreateTripAsync(Guid ownerId, CreateTripRequest request);
        Task<Result> UpdateTripAsync(Guid tripId, Guid requesterId, UpdateTripRequest request);
        Task<Result> DeleteTripAsync(Guid tripId, Guid requesterId);
        Task<Result> AddParticipantAsync(Guid tripId, Guid requesterId, AddParticipantRequest request);
        Task<Result> RemoveParticipantAsync(Guid tripId, Guid requesterId, Guid participantId);
    }
}
