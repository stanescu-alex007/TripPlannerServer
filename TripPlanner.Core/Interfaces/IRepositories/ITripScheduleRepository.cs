using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface ITripScheduleRepository
    {
        Task<List<TripScheduleItem>> GetByTripIdAsync(Guid tripId, CancellationToken ct = default);

        Task<TripScheduleItem?> GetByIdAsync(Guid itemId, CancellationToken ct = default);

        Task<TripScheduleItem> AddAsync(TripScheduleItem item, CancellationToken ct = default);

        Task UpdateAsync(TripScheduleItem item, CancellationToken ct = default);

        /// <summary>Soft delete — the row stays with <c>DeletedAt</c> stamped.</summary>
        Task DeleteAsync(TripScheduleItem item, CancellationToken ct = default);
    }
}
