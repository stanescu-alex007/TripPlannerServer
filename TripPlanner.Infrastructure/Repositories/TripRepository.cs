using Microsoft.EntityFrameworkCore;
using Npgsql;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class TripRepository : ITripRepository
    {
        /// <summary>Postgres SQLSTATE for unique_violation.</summary>
        private const string UniqueViolation = "23505";

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
                .Include(t => t.OwnershipTransfers.Where(o => o.Status == TripOwnershipTransferStatus.Pending))
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
                // The owner's name goes into invitation emails and the trip header.
                .Include(t => t.Owner)
                // Only the offer in progress — the history of past offers is not shown.
                .Include(t => t.OwnershipTransfers.Where(o => o.Status == TripOwnershipTransferStatus.Pending))
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
            tracked.Currency = trip.Currency;
            tracked.TransportType = trip.TransportType;
            tracked.Image = trip.Image;
            tracked.Description = trip.Description;

            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(Guid tripId, Guid? deletedByUserId, CancellationToken ct = default)
        {
            var trip = await _context.Trips
                .Include(t => t.Participants)
                .Include(t => t.ScheduleItems)
                .FirstOrDefaultAsync(t => t.Id == tripId, ct);

            if (trip is null) return;

            // Stamped here rather than through Remove(): the audit interceptor narrows a
            // soft delete to the deletion columns alone, so DeletedByUserId would be
            // dropped on the way out.
            trip.DeletedAt = _clock.GetUtcNow().UtcDateTime;
            trip.DeletedByUserId = deletedByUserId;

            _context.TripScheduleItems.RemoveRange(trip.ScheduleItems);
            _context.TripParticipants.RemoveRange(trip.Participants);

            await _context.SaveChangesAsync(ct);
        }

        public Task<List<Trip>> GetDeletedByUserAsync(
            Guid userId, DateTime since, CancellationToken ct = default) =>
            _context.Trips
                .IgnoreQueryFilters()
                .Where(t => t.DeletedByUserId == userId && t.DeletedAt != null && t.DeletedAt >= since)
                .OrderByDescending(t => t.DeletedAt)
                .AsNoTracking()
                .ToListAsync(ct);

        public Task<Trip?> GetDeletedByIdAsync(Guid tripId, CancellationToken ct = default) =>
            _context.Trips
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tripId && t.DeletedAt != null, ct);

        public async Task RestoreAsync(Trip trip, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var deletedAt = trip.DeletedAt!.Value;

            // Children carry the interceptor's own timestamp, taken a moment after the
            // trip's, so the comparison is ">=". Anything removed before the trip went is
            // left alone: those were separate decisions.
            await _context.TripParticipants
                .IgnoreQueryFilters()
                .Where(p => p.TripId == trip.Id && p.DeletedAt >= deletedAt)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.DeletedAt, (DateTime?)null)
                    .SetProperty(p => p.UpdatedAt, now), ct);

            await _context.TripScheduleItems
                .IgnoreQueryFilters()
                .Where(s => s.TripId == trip.Id && s.DeletedAt >= deletedAt)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.DeletedAt, (DateTime?)null)
                    .SetProperty(i => i.UpdatedAt, now), ct);

            await _context.Trips
                .IgnoreQueryFilters()
                .Where(t => t.Id == trip.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.DeletedAt, (DateTime?)null)
                    .SetProperty(t => t.DeletedByUserId, (Guid?)null)
                    .SetProperty(t => t.UpdatedAt, now), ct);
        }

        public async Task<int> PurgeDeletedBeforeAsync(DateTime cutoff, CancellationToken ct = default)
        {
            var tripIds = await _context.Trips
                .IgnoreQueryFilters()
                .Where(t => t.DeletedAt != null && t.DeletedAt < cutoff)
                .Select(t => t.Id)
                .ToListAsync(ct);

            if (tripIds.Count == 0) return 0;

            var seatIds = await _context.TripParticipants
                .IgnoreQueryFilters()
                .Where(p => tripIds.Contains(p.TripId))
                .Select(p => p.Id)
                .ToListAsync(ct);

            // Deleted in dependency order rather than left to the database's cascades:
            // an ownership offer points at a participant row with no cascade of its own,
            // so it has to go before the seat it names.
            await _context.TripOwnershipTransfers
                .IgnoreQueryFilters()
                .Where(o => tripIds.Contains(o.TripId))
                .ExecuteDeleteAsync(ct);

            await _context.TripInvitations
                .IgnoreQueryFilters()
                .Where(i => seatIds.Contains(i.TripParticipantId))
                .ExecuteDeleteAsync(ct);

            await _context.TripScheduleItems
                .IgnoreQueryFilters()
                .Where(s => tripIds.Contains(s.TripId))
                .ExecuteDeleteAsync(ct);

            await _context.TripParticipants
                .IgnoreQueryFilters()
                .Where(p => tripIds.Contains(p.TripId))
                .ExecuteDeleteAsync(ct);

            await _context.Trips
                .IgnoreQueryFilters()
                .Where(t => tripIds.Contains(t.Id))
                .ExecuteDeleteAsync(ct);

            return tripIds.Count;
        }

        public Task<List<Trip>> GetForExportAsync(Guid userId, CancellationToken ct = default) =>
            _context.Trips
                .Include(t => t.Participants)
                .Include(t => t.ScheduleItems)
                .Where(t => t.OwnerId == userId || t.Participants.Any(p => p.UserId == userId))
                .OrderByDescending(t => t.StartDate)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync(ct);

        public async Task RemoveUserFromAllTripsAsync(Guid userId, CancellationToken ct = default)
        {
            var seats = await _context.TripParticipants
                .Where(p => p.UserId == userId)
                .ToListAsync(ct);

            if (seats.Count == 0) return;

            _context.TripParticipants.RemoveRange(seats);
            await _context.SaveChangesAsync(ct);
        }

        public async Task ReassignParticipantEmailAsync(
            Guid userId, string newEmail, CancellationToken ct = default)
        {
            var seats = await _context.TripParticipants
                .Where(p => p.UserId == userId)
                .ToListAsync(ct);

            var tripsWithSeat = seats.Select(p => p.TripId).ToHashSet();

            // Rows addressed to the new email that are not already this user's.
            var addressedToNew = await _context.TripParticipants
                .Where(p => p.Email == newEmail && p.UserId != userId)
                .ToListAsync(ct);

            foreach (var row in addressedToNew)
            {
                if (tripsWithSeat.Contains(row.TripId))
                    _context.TripParticipants.Remove(row);
                else if (row.UserId is null)
                    row.UserId = userId;
            }

            // Saved on its own, before any address moves. EF orders the statements inside
            // one SaveChanges by its own rules and knows nothing of the index being
            // filtered on DeletedAt, so a combined save could move an email onto a seat
            // that is still live and trip the unique index.
            await _context.SaveChangesAsync(ct);

            foreach (var seat in seats) seat.Email = newEmail;

            await _context.SaveChangesAsync(ct);
        }

        public async Task AddParticipantAsync(TripParticipant participant, CancellationToken ct = default)
        {
            await _context.TripParticipants.AddAsync(participant, ct);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                               { SqlState: UniqueViolation } pg
                                               && pg.ConstraintName?.Contains("Email") == true)
            {
                // Lost a race with a parallel add of the same address. Detached so the
                // rejected insert is not retried by a later save in the same request.
                _context.Entry(participant).State = EntityState.Detached;
                throw new DuplicateParticipantException(participant.TripId, participant.Email, ex);
            }
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
                .Include(p => p.Trip)
                    .ThenInclude(t => t.Owner)
                .FirstOrDefaultAsync(p => p.TripId == tripId && p.Email == email, ct);

        public Task<TripParticipant?> GetParticipantWithTripAsync(
            Guid participantId, CancellationToken ct = default) =>
            _context.TripParticipants
                .Include(p => p.Trip)
                    .ThenInclude(t => t.Owner)
                .FirstOrDefaultAsync(p => p.Id == participantId, ct);

        public Task<int> LinkParticipantsByEmailAsync(
            string email, Guid userId, CancellationToken ct = default) =>
            // Only rows with no account yet: an existing link must never be
            // reassigned by someone registering with the same address later.
            _context.TripParticipants
                .Where(p => p.Email == email && p.UserId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.UserId, userId)
                    .SetProperty(p => p.UpdatedAt, _clock.GetUtcNow().UtcDateTime), ct);

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
