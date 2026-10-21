using TripPlanner.Core.Entities;
using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Issues and redeems the single-use tokens behind emailed links and the two-factor
    /// challenge. Every flow used to repeat the same validation by hand.
    /// </summary>
    public interface IOneTimeTokenService
    {
        /// <summary>
        /// Issues a token after retiring any outstanding one for the same user and
        /// purpose, so only the newest link ever works. Returns the raw value — the only
        /// copy, since just its hash is stored.
        /// </summary>
        Task<string> IssueAsync(
            Guid userId, string purpose, TimeSpan lifetime,
            string? newEmail = null, CancellationToken ct = default);

        /// <summary>
        /// True if a token for this user and purpose went out within the last
        /// <paramref name="window"/>. Flows that email a link check it first, so repeated
        /// clicks or a script cannot flood someone's inbox.
        /// </summary>
        Task<bool> IssuedRecentlyAsync(Guid userId, string purpose, TimeSpan window, CancellationToken ct = default);

        /// <summary>
        /// Finds a token that can still be used — with its user loaded — or reports why it
        /// cannot. Does not consume it.
        /// </summary>
        Task<Result<UserToken>> FindUsableAsync(string rawToken, string purpose, CancellationToken ct = default);

        /// <summary>
        /// Consumes the token in a single conditional update. False means another request
        /// consumed it first, and the caller must not go on to act on it.
        /// </summary>
        Task<bool> TryConsumeAsync(UserToken token, CancellationToken ct = default);
    }
}
