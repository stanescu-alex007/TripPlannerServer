using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface ITripScheduleService
    {
        /// <summary>
        /// Reads a trip's schedule. Takes the requester because access is checked here —
        /// the previous version returned any trip's schedule to any signed-in caller.
        /// </summary>
        Task<Result<List<TripScheduleItemDto>>> GetScheduleAsync(
            Guid tripId, Guid requesterId, CancellationToken ct = default);

        Task<Result<TripScheduleItemDto>> AddItemAsync(
            Guid tripId, Guid requesterId, CreateScheduleItemRequest request, CancellationToken ct = default);

        Task<Result> UpdateItemAsync(
            Guid tripId, Guid itemId, Guid requesterId, UpdateScheduleItemRequest request, CancellationToken ct = default);

        Task<Result> UpdateStatusAsync(
            Guid tripId, Guid itemId, Guid requesterId, string status, CancellationToken ct = default);

        Task<Result> DeleteItemAsync(
            Guid tripId, Guid itemId, Guid requesterId, CancellationToken ct = default);
    }
}
