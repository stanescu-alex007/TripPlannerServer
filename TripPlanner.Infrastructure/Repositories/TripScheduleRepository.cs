using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class TripScheduleRepository : ITripScheduleRepository
    {
        private readonly AppDbContext _context;

        public TripScheduleRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<TripScheduleItem>> GetByTripIdAsync(Guid tripId)
        {
            return await _context.TripScheduleItems
                .Where(s => s.TripId == tripId)
                .OrderBy(s => s.Date)
                .ThenBy(s => s.Order)
                .ThenBy(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<TripScheduleItem?> GetByIdAsync(Guid itemId)
        {
            return await _context.TripScheduleItems.FindAsync(itemId);
        }

        public async Task<TripScheduleItem> AddAsync(TripScheduleItem item)
        {
            _context.TripScheduleItems.Add(item);
            await _context.SaveChangesAsync();
            return item;
        }

        public async Task UpdateAsync(TripScheduleItem item)
        {
            _context.TripScheduleItems.Update(item);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(TripScheduleItem item)
        {
            _context.TripScheduleItems.Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}
