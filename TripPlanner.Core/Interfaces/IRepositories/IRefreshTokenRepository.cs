using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface IRefreshTokenRepository
    {
        /// <summary>
        /// Looks a token up by its SHA-256 hash, including revoked and expired rows —
        /// reuse detection depends on still being able to see a revoked token.
        /// </summary>
        Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

        Task AddAsync(RefreshToken token, CancellationToken ct = default);

        /// <summary>
        /// Revokes <paramref name="current"/> — but only if it is still active, decided by
        /// one conditional statement — then stores <paramref name="replacement"/> and links
        /// the two. False means another request rotated or revoked it first, and nothing
        /// was written.
        /// <para>
        /// Several statements: run it inside <see cref="IUnitOfWork"/>, so a session can
        /// never end up with its old link revoked and no new one stored.
        /// </para>
        /// </summary>
        Task<bool> TryRotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken ct = default);

        /// <summary>
        /// The family's newest active, unexpired link — the one a concurrent refresh
        /// rotated to. Null when the family has no live link left (signed out, revoked,
        /// expired).
        /// </summary>
        Task<RefreshToken?> GetLiveHeadAsync(Guid familyId, CancellationToken ct = default);

        /// <summary>
        /// Revokes every still-active token in a family. Used both for ordinary sign-out
        /// and for detonating the family after a replay is detected.
        /// </summary>
        Task<int> RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct = default);

        /// <summary>Revokes every active token a user has, across all families.</summary>
        Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default);

        /// <summary>Permanently removes rows that expired before <paramref name="cutoff"/>.</summary>
        Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken ct = default);

        /// <summary>
        /// One row per live session: the newest still-valid token of each family, which
        /// is what the user thinks of as "a device I am signed in on".
        /// </summary>
        Task<List<RefreshToken>> GetActiveSessionsAsync(Guid userId, CancellationToken ct = default);

        /// <summary>Revokes one family, but only if it belongs to this user.</summary>
        Task<bool> RevokeFamilyForUserAsync(
            Guid userId, Guid familyId, string reason, CancellationToken ct = default);

        /// <summary>
        /// Whether the session behind an access token is still alive: a live, unrevoked
        /// token in that family, belonging to an account that still exists.
        /// </summary>
        Task<bool> IsSessionLiveAsync(Guid userId, Guid familyId, CancellationToken ct = default);

        /// <summary>Revokes every family except the one given — "sign out everywhere else".</summary>
        Task<int> RevokeAllExceptFamilyAsync(
            Guid userId, Guid keepFamilyId, string reason, CancellationToken ct = default);
    }
}
