using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>Expects an already-normalized (trimmed, lower-cased) address.</summary>
        Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

        Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Resolves many addresses at once, returning only those that have an account.
        /// Replaces a per-address lookup that turned one roster into N round trips.
        /// </summary>
        Task<Dictionary<string, Guid>> GetIdsByEmailsAsync(
            IReadOnlyCollection<string> emails, CancellationToken ct = default);

        /// <summary>
        /// Throws <see cref="Common.DuplicateEmailException"/> when the unique index
        /// rejects the insert.
        /// </summary>
        Task AddAsync(User user, CancellationToken ct = default);

        /// <summary>
        /// Saves the changes made to <paramref name="user"/>. A tracked entity writes only
        /// the properties that actually changed, so values other requests wrote in the
        /// meantime are left alone.
        /// Throws <see cref="Common.DuplicateEmailException"/> when an email change collides
        /// with an address claimed in the meantime.
        /// </summary>
        Task UpdateAsync(User user, CancellationToken ct = default);

        /// <summary>
        /// Counts one failed sign-in: increments the counter in the database and stamps
        /// the time, touching no other column. The loaded entity is updated to match.
        /// </summary>
        Task RecordFailedLoginAsync(User user, CancellationToken ct = default);

        /// <summary>
        /// Clears the failed sign-in counter, touching no other column. The loaded entity
        /// is updated to match.
        /// </summary>
        Task ResetFailedLoginsAsync(User user, CancellationToken ct = default);

        /// <summary>
        /// Soft-deletes the account. The row stays with <c>DeletedAt</c> stamped, but every
        /// credential goes for real: sessions, one-time tokens, recovery codes, external
        /// logins, the password hash and the 2FA secret. None has any history worth
        /// keeping, and left behind they would keep identities claimed by an account
        /// that no longer exists.
        /// Personal data is anonymized as well — name, email, phone, photo, birth date,
        /// and the email and nickname on every trip seat — so nothing left identifies
        /// the person.
        /// </summary>
        Task DeleteAsync(User user, CancellationToken ct = default);

        /// <summary>
        /// Records an authenticator step as spent, but only if it is newer than the last
        /// one. False means the code was already used — one conditional statement, so two
        /// requests presenting the same code cannot both win. On success the loaded
        /// <paramref name="user"/> is updated too, so a later save cannot revert the step.
        /// </summary>
        Task<bool> TryConsumeTwoFactorStepAsync(User user, long timeStep, CancellationToken ct = default);

        /// <summary>Accounts whose 2FA secret does not start with <paramref name="protectedMarker"/>.</summary>
        Task<List<User>> GetWithUnprotectedTwoFactorSecretAsync(
            string protectedMarker, CancellationToken ct = default);

        /// <summary>
        /// Swaps the stored 2FA secret only if it still holds <paramref name="expected"/>,
        /// so a concurrent change — or another instance doing the same work — is never
        /// overwritten.
        /// </summary>
        Task<bool> TryReplaceTwoFactorSecretAsync(
            Guid userId, string expected, string replacement, CancellationToken ct = default);
    }
}
