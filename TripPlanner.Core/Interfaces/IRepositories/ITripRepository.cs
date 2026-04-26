using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface ITripRepository
    {
        Task<List<Trip>> GetAllByOwnerIdAsync(Guid ownerId);
        Task<Trip?> GetByIdAsync(Guid id);
        Task<Trip?> GetByIdWithDetailsAsync(Guid id);
        Task<Trip> CreateAsync(Trip trip);
        Task UpdateAsync(Trip trip);
        Task DeleteAsync(Trip trip);
        Task AddParticipantAsync(TripParticipant participant);
        Task RemoveParticipantAsync(Guid participantId);
        Task<TripParticipant?> GetParticipantByEmailAsync(Guid tripId, string email);
    }
}
