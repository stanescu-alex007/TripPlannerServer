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
        /// Revokes <paramref name="current"/>, links it to <paramref name="replacement"/>
        /// and stores the replacement — in a single transaction, so a session can never
        /// end up with neither token valid.
        /// </summary>
        Task RotateAsync(RefreshToken current, RefreshToken replacement, CancellationToken ct = default);

        /// <summary>
        /// Revokes every still-active token in a family. Used both for ordinary sign-out
        /// and for detonating the family after a replay is detected.
        /// </summary>
        Task<int> RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct = default);

        /// <summary>Revokes every active token a user has, across all families.</summary>
        Task<int> RevokeAllForUserAsync(Guid userId, string reason, CancellationToken ct = default);

        /// <summary>Permanently removes rows that expired before <paramref name="cutoff"/>.</summary>
        Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken ct = default);
    }
}
