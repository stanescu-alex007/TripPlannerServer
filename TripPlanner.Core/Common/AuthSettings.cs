namespace TripPlanner.Core.Common;

/// <summary>
/// Auth-related knobs, bound once at startup and injected as a singleton so the
/// business layer never touches IConfiguration directly.
/// </summary>
public sealed class AuthSettings
{
    // ── JWT ───────────────────────────────────────────────────────────────────
    public string JwtKey { get; init; } = string.Empty;
    public string JwtIssuer { get; init; } = "TripPlanner";
    public string JwtAudience { get; init; } = "TripPlanner";

    /// <summary>Access-token lifetime. Short by design; the refresh token carries the session.</summary>
    public int AccessTokenMinutes { get; init; } = 15;

    /// <summary>Lifetime of a single refresh-token link.</summary>
    public int RefreshTokenDays { get; init; } = 7;

    /// <summary>
    /// Hard ceiling on a whole token family. Rotation cannot extend past it, so a stolen
    /// session cannot be kept alive indefinitely by refreshing.
    /// </summary>
    public int RefreshFamilyDays { get; init; } = 30;

    // ── One-time email tokens ─────────────────────────────────────────────────
    public int EmailVerificationHours { get; init; } = 24;
    public int PasswordResetMinutes { get; init; } = 30;

    // ── Password policy ───────────────────────────────────────────────────────
    /// <summary>Keep in sync with the frontend validator, or users get rejected after the fact.</summary>
    public int PasswordMinLength { get; init; } = 8;
    public int PasswordMaxLength { get; init; } = 200;

    // ── Links inside emails ───────────────────────────────────────────────────
    public string FrontendBaseUrl { get; init; } = "http://localhost:4200";
}
