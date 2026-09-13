using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IJwtService
    {
        /// <summary>
        /// Issues a short-lived access token. Refresh tokens are opaque and come from
        /// <see cref="ISecureTokenService"/> instead — they are not JWTs.
        /// </summary>
        /// <param name="sessionId">
        /// The refresh-token family this access token belongs to, embedded as the "sid"
        /// claim. It is what lets a request identify which session made it without the
        /// refresh cookie, which is scoped to the auth endpoints and never reaches the
        /// rest of the API.
        /// </param>
        string GenerateToken(User user, Guid sessionId);
    }
}
