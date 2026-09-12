using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IJwtService
    {
        /// <summary>
        /// Issues a short-lived access token. Refresh tokens are opaque and come from
        /// <see cref="ISecureTokenService"/> instead — they are not JWTs.
        /// </summary>
        string GenerateToken(User user);
    }
}
