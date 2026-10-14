using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>Keeping a session alive through refresh-token rotation, and ending it.</summary>
    public interface ISessionService
    {
        /// <summary>
        /// Rotates the refresh token. Presenting a link that was already rotated revokes
        /// its whole family, since only a leaked copy can be replayed — unless it was
        /// rotated within the last few seconds (<c>AuthSettings.RefreshReuseGraceSeconds</c>),
        /// which is what two tabs refreshing at once look like. That request is served
        /// from the family's current link instead.
        /// </summary>
        Task<Result<AuthTokens>> RefreshAsync(
            string rawRefreshToken, RequestContext context, CancellationToken ct = default);

        Task<Result> LogoutAsync(string? rawRefreshToken, CancellationToken ct = default);
    }
}
