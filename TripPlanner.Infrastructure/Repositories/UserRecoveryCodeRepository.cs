using Microsoft.EntityFrameworkCore;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class UserRecoveryCodeRepository : IUserRecoveryCodeRepository
    {
        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public UserRecoveryCodeRepository(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<List<UserRecoveryCode>> GetUnusedAsync(Guid userId, CancellationToken ct = default) =>
            _context.UserRecoveryCodes
                .Where(c => c.UserId == userId && c.UsedAt == null)
                .ToListAsync(ct);

        public Task<int> CountUnusedAsync(Guid userId, CancellationToken ct = default) =>
            _context.UserRecoveryCodes
                .CountAsync(c => c.UserId == userId && c.UsedAt == null, ct);

        public async Task ReplaceAllAsync(
            Guid userId, IEnumerable<UserRecoveryCode> codes, CancellationToken ct = default)
        {
            // Regenerating must invalidate the previous set: a code printed months ago
            // has to stop working the moment a new sheet is issued.
            await _context.UserRecoveryCodes
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(ct);

            await _context.UserRecoveryCodes.AddRangeAsync(codes, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task MarkUsedAsync(Guid codeId, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // Bulk update, so UpdatedAt is maintained here rather than by the interceptor.
            await _context.UserRecoveryCodes
                .Where(c => c.Id == codeId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.UsedAt, now)
                    .SetProperty(c => c.UpdatedAt, now), ct);
        }

        public async Task DeleteAllAsync(Guid userId, CancellationToken ct = default)
        {
            await _context.UserRecoveryCodes
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(ct);
        }
    }
}
