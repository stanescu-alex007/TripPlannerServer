using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class TripRepository : ITripRepository
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public TripRepository(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<List<Trip>> GetAccessibleByUserAsync(Guid userId, CancellationToken ct = default) =>
            _context.Trips
                .Include(t => t.Participants)
                .Where(t => t.OwnerId == userId
                            || t.Participants.Any(p => p.UserId == userId
                                                       && p.Status != TripParticipantStatus.Declined))
                .OrderByDescending(t => t.StartDate)
                .AsNoTracking()
                .ToListAsync(ct);

        public Task<Trip?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            _context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

        public Task<Trip?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default) =>
            _context.Trips
                .Include(t => t.Participants)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id, ct);

        public async Task<Trip> CreateAsync(Trip trip, CancellationToken ct = default)
        {
            await _context.Trips.AddAsync(trip, ct);
            await _context.SaveChangesAsync(ct);
            return trip;
        }

        public async Task UpdateAsync(Trip trip, CancellationToken ct = default)
        {
            // Attach-and-mark rather than ExecuteUpdate, so the audit interceptor runs and
            // UpdatedAt is maintained without being restated at every call site.
            var tracked = await _context.Trips.FirstOrDefaultAsync(t => t.Id == trip.Id, ct);
            if (tracked is null) return;

            tracked.Name = trip.Name;
            tracked.Location = trip.Location;
            tracked.StartDate = trip.StartDate;
            tracked.EndDate = trip.EndDate;
            tracked.Budget = trip.Budget;
            tracked.TransportType = trip.TransportType;
            tracked.Image = trip.Image;
            tracked.Description = trip.Description;

            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid tripId, CancellationToken ct = default)
        {
            var trip = await _context.Trips
                .Include(t => t.Participants)
                .Include(t => t.ScheduleItems)
                .FirstOrDefaultAsync(t => t.Id == tripId, ct);

            if (trip is null) return;

            // Soft delete means no row is actually removed, so the database's FK cascade
            // never fires — children have to be marked explicitly or they would linger,
            // visible to any query that reaches them directly.
            _context.TripScheduleItems.RemoveRange(trip.ScheduleItems);
            _context.TripParticipants.RemoveRange(trip.Participants);
            _context.Trips.Remove(trip);

            await _context.SaveChangesAsync(ct);
        }

        public async Task AddParticipantAsync(TripParticipant participant, CancellationToken ct = default)
        {
            await _context.TripParticipants.AddAsync(participant, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateParticipantAsync(TripParticipant participant, CancellationToken ct = default)
        {
            _context.TripParticipants.Update(participant);
            await _context.SaveChangesAsync(ct);
        }

        public async Task RemoveParticipantAsync(Guid participantId, CancellationToken ct = default)
        {
            var participant = await _context.TripParticipants
                .FirstOrDefaultAsync(p => p.Id == participantId, ct);

            if (participant is null) return;

            _context.TripParticipants.Remove(participant);
            await _context.SaveChangesAsync(ct);
        }

        public Task<TripParticipant?> GetParticipantByEmailAsync(
            Guid tripId, string email, CancellationToken ct = default) =>
            // Plain equality: callers normalize first, which keeps the unique
            // (TripId, Email) index usable instead of forcing a scan over lower(Email).
            _context.TripParticipants
                .FirstOrDefaultAsync(p => p.TripId == tripId && p.Email == email, ct);

        public async Task SyncParticipantsAsync(
            Guid tripId, IReadOnlyCollection<TripParticipant> desired, CancellationToken ct = default)
        {
            var existing = await _context.TripParticipants
                .Where(p => p.TripId == tripId)
                .ToListAsync(ct);

            var owner = existing.FirstOrDefault(p => p.Role == TripParticipantRole.Owner);
            var desiredByEmail = desired.ToDictionary(p => p.Email, StringComparer.OrdinalIgnoreCase);

            foreach (var current in existing)
            {
                // The owner row is never touched by a roster edit.
                if (current.Id == owner?.Id) continue;

                if (desiredByEmail.TryGetValue(current.Email, out var wanted))
                {
                    // Still on the trip: update the editable fields but keep Status, so an
                    // accepted invitation is not silently reset to pending on every save.
                    current.Nickname = wanted.Nickname;
                    current.Role = wanted.Role;
                    current.UserId = wanted.UserId ?? current.UserId;

                    desiredByEmail.Remove(current.Email);
                }
                else
                {
                    _context.TripParticipants.Remove(current);
                }
            }

            // Whatever is left is genuinely new.
            foreach (var addition in desiredByEmail.Values)
            {
                addition.TripId = tripId;
                await _context.TripParticipants.AddAsync(addition, ct);
            }

            // One SaveChanges → one transaction for the whole roster change.
            await _context.SaveChangesAsync(ct);
        }
    }
}
