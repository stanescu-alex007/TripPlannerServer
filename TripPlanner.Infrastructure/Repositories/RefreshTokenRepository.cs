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
    }
}
