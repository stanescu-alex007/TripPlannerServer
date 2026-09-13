using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public RefreshTokenRepository(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
            _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        {
            await _context.RefreshTokens.AddAsync(token, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task RotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            current.RevokedAt = now;
            current.RevokedReason = RefreshTokenRevokeReason.Rotated;
            current.ReplacedByTokenId = replacement.Id;

            await _context.RefreshTokens.AddAsync(replacement, ct);

            // A single SaveChanges — EF wraps both statements in one transaction, so the
            // old token can never be revoked without the new one being written.
            await _context.SaveChangesAsync(ct);
        }

        public Task<int> RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // ExecuteUpdate skips the change tracker — and therefore the audit
            // interceptor — so UpdatedAt has to be set explicitly here.
            return _context.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedReason, reason)
                    .SetProperty(t => t.UpdatedAt, now), ct);
        }

        public Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            return _context.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedReason, reason)
                    .SetProperty(t => t.UpdatedAt, now), ct);
        }

        public Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken ct = default) =>
            _context.RefreshTokens
                .Where(t => t.ExpiresAt < cutoff)
                .ExecuteDeleteAsync(ct);

        public async Task<List<RefreshToken>> GetActiveSessionsAsync(
            Guid userId, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // A family is a chain of rotated tokens; only its newest live link
            // represents the session, so the rest would show as duplicate devices.
            var live = await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
                .AsNoTracking()
                .ToListAsync(ct);

            return live
                .GroupBy(t => t.FamilyId)
                .Select(family => family.OrderByDescending(t => t.CreatedAt).First())
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
        }

        public async Task<bool> RevokeFamilyForUserAsync(
            Guid userId, Guid familyId, string reason, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // Filtering on UserId as well as FamilyId is what stops one user ending
            // another user's session by guessing an id.
            var affected = await _context.RefreshTokens
                .Where(t => t.UserId == userId && t.FamilyId == familyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedReason, reason)
                    .SetProperty(t => t.UpdatedAt, now), ct);

            return affected > 0;
        }

        public Task<int> RevokeAllExceptFamilyAsync(
            Guid userId, Guid keepFamilyId, string reason, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            return _context.RefreshTokens
                .Where(t => t.UserId == userId && t.FamilyId != keepFamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedReason, reason)
                    .SetProperty(t => t.UpdatedAt, now), ct);
        }
    }
}
