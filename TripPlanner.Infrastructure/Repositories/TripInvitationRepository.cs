using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class TripInvitationRepository : ITripInvitationRepository
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public TripInvitationRepository(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<TripInvitation?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
            _context.TripInvitations
                .Include(i => i.Participant)
                    .ThenInclude(p => p.Trip)
                        .ThenInclude(t => t.Owner)
                .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

        public Task<TripInvitation?> GetOpenForParticipantAsync(
            Guid participantId, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            return _context.TripInvitations
                .Where(i => i.TripParticipantId == participantId
                            && i.RespondedAt == null
                            && i.InvalidatedAt == null
                            && i.ExpiresAt > now)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        public async Task AddAsync(TripInvitation invitation, CancellationToken ct = default)
        {
            await _context.TripInvitations.AddAsync(invitation, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateAsync(TripInvitation invitation, CancellationToken ct = default)
        {
            _context.TripInvitations.Update(invitation);
            await _context.SaveChangesAsync(ct);
        }

        public Task<int> InvalidateForParticipantAsync(Guid participantId, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // Bulk update bypasses the audit interceptor, so UpdatedAt is set here.
            return _context.TripInvitations
                .Where(i => i.TripParticipantId == participantId
                            && i.RespondedAt == null
                            && i.InvalidatedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.InvalidatedAt, now)
                    .SetProperty(i => i.UpdatedAt, now), ct);
        }

        public Task<int> InvalidateOpenForTripAsync(Guid tripId, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // Bulk update bypasses the audit interceptor, so UpdatedAt is set here.
            return _context.TripInvitations
                .Where(i => i.Participant.TripId == tripId
                            && i.RespondedAt == null
                            && i.InvalidatedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.InvalidatedAt, now)
                    .SetProperty(i => i.UpdatedAt, now), ct);
        }

        public Task<List<TripInvitation>> GetOpenForEmailAsync(
            string email, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            return _context.TripInvitations
                .Include(i => i.Participant)
                    .ThenInclude(p => p.Trip)
                        .ThenInclude(t => t.Owner)
                .Where(i => i.Participant.Email == email
                            && i.RespondedAt == null
                            && i.InvalidatedAt == null
                            && i.ExpiresAt > now)
                .OrderByDescending(i => i.CreatedAt)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken ct = default) =>
            _context.TripInvitations
                .Where(i => i.ExpiresAt < cutoff)
                .ExecuteDeleteAsync(ct);
    }
}
