using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface ITripScheduleService
    {
        Task<List<TripScheduleItemDto>> GetScheduleAsync(Guid tripId);
        Task<Result<TripScheduleItemDto>> AddItemAsync(Guid tripId, Guid requesterId, CreateScheduleItemRequest request);
        Task<Result> UpdateItemAsync(Guid itemId, Guid requesterId, UpdateScheduleItemRequest request);
        Task<Result> UpdateStatusAsync(Guid itemId, Guid requesterId, string status);
        Task<Result> DeleteItemAsync(Guid itemId, Guid requesterId);
    }
}
