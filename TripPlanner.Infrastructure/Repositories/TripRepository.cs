using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class TripRepository : ITripRepository
    {
        private readonly AppDbContext _context;

        public TripRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Trip>> GetAllByOwnerIdAsync(Guid ownerId)
        {
            return await _context.Trips
                .Include(t => t.Participants)
                    .ThenInclude(p => p.User)
                .Where(t => t.OwnerId == ownerId)
                .OrderByDescending(t => t.StartDate)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Trip?> GetByIdAsync(Guid id)
        {
            return await _context.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Trip?> GetByIdWithDetailsAsync(Guid id)
        {
            return await _context.Trips
                .Include(t => t.Participants)
                    .ThenInclude(p => p.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Trip> CreateAsync(Trip trip)
        {
            trip.Id = Guid.NewGuid();
            await _context.Trips.AddAsync(trip);
            await _context.SaveChangesAsync();
            return trip;
        }

        public async Task UpdateAsync(Trip trip)
        {
            // Use ExecuteUpdateAsync to avoid graph-tracking issues from AsNoTracking loads.
            // Only scalar Trip properties are updated — participants are managed separately.
            await _context.Trips
                .Where(t => t.Id == trip.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Name, trip.Name)
                    .SetProperty(t => t.Location, trip.Location)
                    .SetProperty(t => t.StartDate, trip.StartDate)
                    .SetProperty(t => t.EndDate, trip.EndDate)
                    .SetProperty(t => t.Budget, trip.Budget)
                    .SetProperty(t => t.TransportType, trip.TransportType)
                    .SetProperty(t => t.Image, trip.Image)
                    .SetProperty(t => t.Description, trip.Description));
        }

        public async Task DeleteAsync(Trip trip)
        {
            // Attach before removing in case the entity was loaded with AsNoTracking
            _context.Trips.Attach(trip);
            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
        }

        public async Task AddParticipantAsync(TripParticipant participant)
        {
            await _context.TripParticipants.AddAsync(participant);
            await _context.SaveChangesAsync();
        }

        public async Task RemoveParticipantAsync(Guid participantId)
        {
            var participant = await _context.TripParticipants
                .FirstOrDefaultAsync(p => p.Id == participantId);

            if (participant != null)
            {
                _context.TripParticipants.Remove(participant);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<TripParticipant?> GetParticipantByEmailAsync(Guid tripId, string email)
        {
            return await _context.TripParticipants
                .FirstOrDefaultAsync(p => p.TripId == tripId &&
                    p.Email.ToLower() == email.ToLower());
        }
    }
}
