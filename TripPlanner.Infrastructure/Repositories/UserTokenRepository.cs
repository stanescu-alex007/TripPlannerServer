using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class UserTokenRepository : IUserTokenRepository
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public UserTokenRepository(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<UserToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
            _context.UserTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        public async Task AddAsync(UserToken token, CancellationToken ct = default)
        {
            await _context.UserTokens.AddAsync(token, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<bool> TryMarkUsedAsync(Guid tokenId, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // The WHERE clause is the guard: of two concurrent redemptions, only one
            // statement can still match. Bulk update, so UpdatedAt is set here.
            var affected = await _context.UserTokens
                .Where(t => t.Id == tokenId && t.UsedAt == null && t.InvalidatedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.UsedAt, now)
                    .SetProperty(t => t.UpdatedAt, now), ct);

            return affected > 0;
        }

        public Task<int> InvalidateOutstandingAsync(Guid userId, string purpose, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // Bulk update — bypasses the audit interceptor, so UpdatedAt is set here.
            return _context.UserTokens
                .Where(t => t.UserId == userId
                            && t.Purpose == purpose
                            && t.UsedAt == null
                            && t.InvalidatedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.InvalidatedAt, now)
                    .SetProperty(t => t.UpdatedAt, now), ct);
        }

        public Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken ct = default) =>
            _context.UserTokens
                .Where(t => t.ExpiresAt < cutoff)
                .ExecuteDeleteAsync(ct);
    }
}
