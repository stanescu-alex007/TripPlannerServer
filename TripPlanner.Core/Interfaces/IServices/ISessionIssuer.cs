using TripPlanner.Core.Entities;
using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// The one place access and refresh tokens are minted. Sign-in, two-factor, password
    /// change and refresh all end in a session, and each used to assemble it by hand.
    /// </summary>
    public interface ISessionIssuer
    {
        /// <summary>Starts a new token family — a new session on a new device.</summary>
        Task<AuthTokens> StartAsync(User user, RequestContext context, CancellationToken ct = default);

        /// <summary>
        /// Revokes <paramref name="current"/> and issues the next link in its family,
        /// never outliving the expiry of the family itself. Null when another request
        /// rotated or revoked <paramref name="current"/> first — nothing is issued then.
        /// </summary>
        Task<AuthTokens?> TryRotateAsync(
            User user, RefreshToken current, RequestContext context, CancellationToken ct = default);
    }
}
