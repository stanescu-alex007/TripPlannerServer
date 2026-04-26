using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface ITripScheduleRepository
    {
        Task<List<TripScheduleItem>> GetByTripIdAsync(Guid tripId);
        Task<TripScheduleItem?> GetByIdAsync(Guid itemId);
        Task<TripScheduleItem> AddAsync(TripScheduleItem item);
        Task UpdateAsync(TripScheduleItem item);
        Task DeleteAsync(TripScheduleItem item);
    }
}
