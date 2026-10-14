using System.Linq.Expressions;
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

        public async Task<bool> TryRotateAsync(
            RefreshToken current, RefreshToken replacement, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // The claim is one conditional statement. Revoking through the tracked entity
            // decided "still active" from a value read earlier, so two requests carrying
            // the same cookie could both pass that check and both rotate, forking the
            // family into two live chains. Here the row lock settles it: the second
            // statement waits for the first transaction, then matches nothing.
            var claimed = await _context.RefreshTokens
                .Where(t => t.Id == current.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.RevokedReason, RefreshTokenRevokeReason.Rotated)
                    .SetProperty(t => t.UpdatedAt, now), ct);

            if (claimed == 0) return false;

            await _context.RefreshTokens.AddAsync(replacement, ct);
            await _context.SaveChangesAsync(ct);

            // Linked only now: the FK needs the replacement row to exist, and the claim
            // had to come first so a lost race writes nothing at all.
            await _context.RefreshTokens
                .Where(t => t.Id == current.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.ReplacedByTokenId, replacement.Id), ct);

            // The bulk statements bypassed the change tracker; bring the loaded entity in
            // line so nothing later in the request writes the stale "active" state back.
            AcceptStoreValue(current, t => t.RevokedAt, now);
            AcceptStoreValue(current, t => t.RevokedReason, RefreshTokenRevokeReason.Rotated);
            AcceptStoreValue(current, t => t.UpdatedAt, now);
            AcceptStoreValue(current, t => t.ReplacedByTokenId, replacement.Id);

            return true;
        }

        public Task<RefreshToken?> GetLiveHeadAsync(Guid familyId, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            return _context.RefreshTokens
                .Where(t => t.FamilyId == familyId && t.RevokedAt == null && t.ExpiresAt > now)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(ct);
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

        /// <summary>
        /// Records values a bulk statement already wrote, as both current and original,
        /// so the change tracker does not treat them as pending edits.
        /// </summary>
        private void AcceptStoreValue<T>(
            RefreshToken token, Expression<Func<RefreshToken, T>> property, T value)
        {
            var entry = _context.Entry(token);
            var prop = entry.Property(property);

            prop.CurrentValue = value;

            if (entry.State is EntityState.Detached or EntityState.Added) return;

            prop.OriginalValue = value;
            prop.IsModified = false;
        }

        public Task<bool> IsSessionLiveAsync(
            Guid userId, Guid familyId, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // The query filter drops rows belonging to deleted accounts, so a deleted
            // user fails here too.
            return _context.RefreshTokens
                .AsNoTracking()
                .AnyAsync(t => t.UserId == userId
                               && t.FamilyId == familyId
                               && t.RevokedAt == null
                               && t.ExpiresAt > now, ct);
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
