using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        /// <summary>Postgres SQLSTATE for unique_violation.</summary>
        private const string UniqueViolation = "23505";

        private readonly AppDbContext _context;
        private readonly TimeProvider _clock;

        public UserRepository(AppDbContext context, TimeProvider clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
            _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
            _context.Users.AnyAsync(u => u.Email == email, ct);

        public async Task<Dictionary<string, Guid>> GetIdsByEmailsAsync(
            IReadOnlyCollection<string> emails, CancellationToken ct = default)
        {
            if (emails.Count == 0) return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            var rows = await _context.Users
                .Where(u => emails.Contains(u.Email))
                .Select(u => new { u.Email, u.Id })
                .AsNoTracking()
                .ToListAsync(ct);

            return rows.ToDictionary(r => r.Email, r => r.Id, StringComparer.OrdinalIgnoreCase);
        }

        public async Task AddAsync(User user, CancellationToken ct = default)
        {
            await _context.Users.AddAsync(user, ct);

            // Can lose a race against a concurrent registration for the same address.
            await SaveTranslatingDuplicateEmailAsync(user, ct);
        }

        public async Task UpdateAsync(User user, CancellationToken ct = default)
        {
            // Update() is only for an entity this context is not tracking. On a tracked
            // one it marks EVERY column modified, so the save writes back whatever this
            // request loaded — silently undoing changes made since by someone else: a
            // TOTP step spent through the replay guard, or a password or email changed
            // in another request while this one was only bumping a counter. Tracked
            // entities already know which properties changed; saving writes just those.
            if (_context.Entry(user).State == EntityState.Detached)
                _context.Users.Update(user);

            // Only an email change can collide: the address was free when the change was
            // requested, but someone may have registered it since.
            await SaveTranslatingDuplicateEmailAsync(user, ct);
        }

        public async Task DeleteAsync(User user, CancellationToken ct = default)
        {
            await _context.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);
            await _context.UserTokens.Where(t => t.UserId == user.Id).ExecuteDeleteAsync(ct);
            await _context.UserRecoveryCodes.Where(c => c.UserId == user.Id).ExecuteDeleteAsync(ct);
            await _context.ExternalLogins.Where(e => e.UserId == user.Id).ExecuteDeleteAsync(ct);

            user.PasswordHash = null;
            user.TwoFactorSecret = null;
            user.TwoFactorEnabledAt = null;

            // Anonymized, not just hidden: GDPR's right to erasure means the person can
            // no longer be identified from what is left. The row stays only so trips,
            // seats and history that point at it keep a valid reference.
            var placeholderEmail = AnonymizedEmail(user.Id);
            user.FirstName = "Deleted";
            user.LastName = "user";
            user.Email = placeholderEmail;
            user.Phone = null;
            user.ImageUrl = null;
            user.BirthDate = null;
            user.FailedLoginAttempts = 0;
            user.LastFailedLoginAt = null;

            var now = _clock.GetUtcNow().UtcDateTime;

            // Seats on other people's trips carry the address and the nickname too —
            // including seats removed earlier, which the query filter would hide.
            // Bulk update — bypasses the audit interceptor, so UpdatedAt is set here.
            await _context.TripParticipants
                .IgnoreQueryFilters()
                .Where(p => p.UserId == user.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Email, placeholderEmail)
                    .SetProperty(p => p.Nickname, "Deleted user")
                    .SetProperty(p => p.UpdatedAt, now), ct);

            // Stamped here rather than through Remove(). Remove runs EF's in-memory
            // cascade before any interceptor sees it: tracked participant rows would have
            // their UserId nulled, and the Restrict relationship to owned trips would
            // throw — neither of which a soft delete wants.
            user.DeletedAt = now;

            // Same reasoning as UpdateAsync: only a detached entity needs Update().
            if (_context.Entry(user).State == EntityState.Detached)
                _context.Users.Update(user);

            await _context.SaveChangesAsync(ct);
        }

        public async Task<bool> TryConsumeTwoFactorStepAsync(
            User user, long timeStep, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // Bulk update — bypasses the audit interceptor, so UpdatedAt is set here.
            var affected = await _context.Users
                .Where(u => u.Id == user.Id
                            && (u.TwoFactorLastStep == null || u.TwoFactorLastStep < timeStep))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.TwoFactorLastStep, timeStep)
                    .SetProperty(u => u.UpdatedAt, now), ct);

            if (affected == 0) return false;

            // The bulk statement bypassed the change tracker, so the loaded entity still
            // holds the old step. Brought in line so no later save in this request can
            // carry the stale value back and reopen the replay window.
            AcceptStoreValue(user, u => u.TwoFactorLastStep, timeStep);
            AcceptStoreValue(user, u => u.UpdatedAt, now);

            return true;
        }

        public async Task RecordFailedLoginAsync(User user, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // Bulk update — bypasses the audit interceptor, so UpdatedAt is set here.
            // Incremented in the database, not read-modify-written: parallel wrong
            // guesses each count, instead of all writing back the same "n + 1". And only
            // the throttle columns are touched, so a counter bump can never overwrite a
            // password or email another request changed after this one loaded the row.
            await _context.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.FailedLoginAttempts, u => u.FailedLoginAttempts + 1)
                    .SetProperty(u => u.LastFailedLoginAt, now)
                    .SetProperty(u => u.UpdatedAt, now), ct);

            // Mirrored locally so the caller sees the new state without a re-read.
            AcceptStoreValue(user, u => u.FailedLoginAttempts, user.FailedLoginAttempts + 1);
            AcceptStoreValue(user, u => u.LastFailedLoginAt, now);
            AcceptStoreValue(user, u => u.UpdatedAt, now);
        }

        public async Task ResetFailedLoginsAsync(User user, CancellationToken ct = default)
        {
            // Targeted for the same reason as RecordFailedLoginAsync. Notably this runs
            // right after the TOTP replay guard spent a step, and a whole-row write here
            // used to put the previous step straight back.
            // Bulk update — bypasses the audit interceptor, so UpdatedAt is set here.
            var now = _clock.GetUtcNow().UtcDateTime;

            await _context.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.FailedLoginAttempts, 0)
                    .SetProperty(u => u.LastFailedLoginAt, (DateTime?)null)
                    .SetProperty(u => u.UpdatedAt, now), ct);

            AcceptStoreValue(user, u => u.FailedLoginAttempts, 0);
            AcceptStoreValue(user, u => u.LastFailedLoginAt, null);
            AcceptStoreValue(user, u => u.UpdatedAt, now);
        }

        public Task<List<User>> GetWithUnprotectedTwoFactorSecretAsync(
            string protectedMarker, CancellationToken ct = default) =>
            _context.Users
                .Where(u => u.TwoFactorSecret != null && !u.TwoFactorSecret.StartsWith(protectedMarker))
                .AsNoTracking()
                .ToListAsync(ct);

        public async Task<bool> TryReplaceTwoFactorSecretAsync(
            Guid userId, string expected, string replacement, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            // Bulk update — bypasses the audit interceptor, so UpdatedAt is set here.
            var affected = await _context.Users
                .Where(u => u.Id == userId && u.TwoFactorSecret == expected)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.TwoFactorSecret, replacement)
                    .SetProperty(u => u.UpdatedAt, now), ct);

            return affected > 0;
        }

        /// <summary>
        /// Unique per account, so the filtered unique indexes never see two equal values,
        /// and on the reserved <c>.invalid</c> domain, so nothing can ever be mailed to it.
        /// </summary>
        private static string AnonymizedEmail(Guid userId) => $"deleted-{userId:N}@deleted.invalid";

        /// <summary>
        /// Records a value a bulk statement has already written. Both the current and the
        /// original value are set, so the change tracker treats the property as unchanged
        /// rather than as a pending edit to write again on the next save.
        /// </summary>
        private void AcceptStoreValue<T>(User user, Expression<Func<User, T>> property, T value)
        {
            var entry = _context.Entry(user);

            if (entry.State is EntityState.Detached or EntityState.Added)
            {
                entry.Property(property).CurrentValue = value;
                return;
            }

            var prop = entry.Property(property);
            prop.CurrentValue = value;
            prop.OriginalValue = value;
            prop.IsModified = false;
        }

        private async Task SaveTranslatingDuplicateEmailAsync(User user, CancellationToken ct)
        {
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                               { SqlState: UniqueViolation } pg
                                               && pg.ConstraintName?.Contains("Email") == true)
            {
                // Detached so the rejected state is not retried by a later save in the
                // same request.
                _context.Entry(user).State = EntityState.Detached;
                throw new DuplicateEmailException(user.Email, ex);
            }
        }
    }
}
