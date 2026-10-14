using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class TripOwnershipRepository : ITripOwnershipRepository
    {
        private readonly AppDbContext _context;

        public TripOwnershipRepository(AppDbContext context) => _context = context;

        public Task<List<Trip>> GetOwnedTripsAsync(Guid ownerId, CancellationToken ct = default) =>
            _context.Trips
                .Include(t => t.Participants)
                .Where(t => t.OwnerId == ownerId)
                .ToListAsync(ct);

        public Task<TripOwnershipTransfer?> GetByIdAsync(Guid transferId, CancellationToken ct = default) =>
            _context.TripOwnershipTransfers
                .Include(o => o.Candidate)
                .Include(o => o.Trip)
                    .ThenInclude(t => t.Participants)
                .FirstOrDefaultAsync(o => o.Id == transferId, ct);

        public Task<List<TripOwnershipTransfer>> GetPendingForUserAsync(
            Guid userId, CancellationToken ct = default) =>
            _context.TripOwnershipTransfers
                .Include(o => o.Candidate)
                .Include(o => o.Trip)
                    .ThenInclude(t => t.Participants)
                .Where(o => o.Status == TripOwnershipTransferStatus.Pending
                            && o.Candidate.UserId == userId)
                .OrderBy(o => o.ExpiresAt)
                .ToListAsync(ct);

        public Task<List<Guid>> GetOverdueIdsAsync(DateTime now, CancellationToken ct = default) =>
            _context.TripOwnershipTransfers
                .Where(o => o.Status == TripOwnershipTransferStatus.Pending && o.ExpiresAt <= now)
                .Select(o => o.Id)
                .ToListAsync(ct);

        public async Task<HashSet<Guid>> GetPassedOverParticipantIdsAsync(
            Guid tripId, CancellationToken ct = default)
        {
            // Anyone asked before the trip last changed hands gets a fresh chance: the
            // circumstances they said no under are gone.
            var lastHandover = await _context.TripOwnershipTransfers
                .Where(o => o.TripId == tripId && o.Status == TripOwnershipTransferStatus.Accepted)
                .MaxAsync(o => (DateTime?)o.RespondedAt, ct);

            var ids = await _context.TripOwnershipTransfers
                .Where(o => o.TripId == tripId
                            && o.Status != TripOwnershipTransferStatus.Pending
                            && o.Status != TripOwnershipTransferStatus.Accepted
                            && (lastHandover == null || o.CreatedAt > lastHandover))
                .Select(o => o.CandidateParticipantId)
                .ToListAsync(ct);

            return ids.ToHashSet();
        }

        public async Task AddAsync(TripOwnershipTransfer transfer, CancellationToken ct = default)
        {
            await _context.TripOwnershipTransfers.AddAsync(transfer, ct);
            await SaveAsync(ct);
        }

        public async Task SaveAsync(CancellationToken ct = default)
        {
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConcurrencyConflictException(ex);
            }
        }
    }
}
