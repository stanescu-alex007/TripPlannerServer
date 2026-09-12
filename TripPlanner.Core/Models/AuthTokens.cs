namespace TripPlanner.Core.Models
{
    /// <summary>
    /// A freshly issued token pair.
    /// <para>
    /// <see cref="RefreshToken"/> is the raw value and exists only long enough for the
    /// controller to put it in an HttpOnly cookie — it is never returned in a response
    /// body and only its hash reaches the database.
    /// </para>
    /// </summary>
    public sealed record AuthTokens(
        string AccessToken,
        DateTime AccessTokenExpiresAt,
        string RefreshToken,
        DateTime RefreshTokenExpiresAt);
}
