using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class TripScheduleRepository : ITripScheduleRepository
    {
        private readonly AppDbContext _context;

        public TripScheduleRepository(AppDbContext context) => _context = context;

        public Task<List<TripScheduleItem>> GetByTripIdAsync(Guid tripId, CancellationToken ct = default) =>
            _context.TripScheduleItems
                .Where(s => s.TripId == tripId)
                .OrderBy(s => s.Date)
                .ThenBy(s => s.Order)
                .ThenBy(s => s.CreatedAt)
                .AsNoTracking()
                .ToListAsync(ct);

        public Task<TripScheduleItem?> GetByIdAsync(Guid itemId, CancellationToken ct = default) =>
            // FirstOrDefault rather than Find: Find bypasses the soft-delete query filter
            // and would happily return a deleted row straight from the change tracker.
            _context.TripScheduleItems.FirstOrDefaultAsync(s => s.Id == itemId, ct);

        public async Task<TripScheduleItem> AddAsync(TripScheduleItem item, CancellationToken ct = default)
        {
            await _context.TripScheduleItems.AddAsync(item, ct);
            await _context.SaveChangesAsync(ct);
            return item;
        }

        public async Task UpdateAsync(TripScheduleItem item, CancellationToken ct = default)
        {
            _context.TripScheduleItems.Update(item);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(TripScheduleItem item, CancellationToken ct = default)
        {
            _context.TripScheduleItems.Remove(item);
            await _context.SaveChangesAsync(ct);
        }
    }
}
