using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface IUserTokenRepository
    {
        /// <summary>
        /// Finds a one-time token by hash, including used and expired rows, so the caller
        /// can tell "already used" apart from "never existed".
        /// </summary>
        Task<UserToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

        Task AddAsync(UserToken token, CancellationToken ct = default);

        /// <summary>
        /// Stamps <c>UsedAt</c> only if the token is still unused and not invalidated.
        /// One conditional statement, so two requests racing on the same link cannot
        /// both succeed.
        /// </summary>
        Task<bool> TryMarkUsedAsync(Guid tokenId, CancellationToken ct = default);

        /// <summary>
        /// Retires every outstanding token a user holds for one purpose. Called before
        /// issuing a new one, so requesting a second reset link kills the first.
        /// </summary>
        Task<int> InvalidateOutstandingAsync(Guid userId, string purpose, CancellationToken ct = default);

        /// <summary>True if a token for this user and purpose was issued at or after <paramref name="since"/>.</summary>
        Task<bool> IssuedSinceAsync(Guid userId, string purpose, DateTime since, CancellationToken ct = default);

        Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken ct = default);
    }
}
