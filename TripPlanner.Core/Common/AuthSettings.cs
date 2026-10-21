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

    /// <summary>
    /// How long after a link is rotated it may still be presented without counting as a
    /// replay. Two tabs refreshing with the same cookie at once is normal browser
    /// behaviour, not theft: the second request finds the link already rotated, and
    /// without this window it detonated the whole family and signed the user out
    /// everywhere. Inside the window the request is served from the family's current
    /// link instead. Keep it short — it is also how long a stolen, already-rotated link
    /// stays usable.
    /// </summary>
    public int RefreshReuseGraceSeconds { get; init; } = 30;

    // ── One-time email tokens ─────────────────────────────────────────────────
    public int EmailVerificationHours { get; init; } = 24;
    public int PasswordResetMinutes { get; init; } = 30;

    /// <summary>
    /// Minimum gap between two verification or reset emails to the same account. The
    /// per-IP rate limit alone still let ten emails hit one inbox in a few seconds.
    /// Keep in sync with the frontend's resend countdown.
    /// </summary>
    public static readonly TimeSpan EmailResendCooldown = TimeSpan.FromSeconds(60);

    // ── Password policy ───────────────────────────────────────────────────────
    /// <summary>Keep in sync with the frontend validator, or users get rejected after the fact.</summary>
    public int PasswordMinLength { get; init; } = 8;
    public int PasswordMaxLength { get; init; } = 200;

    // ── Failed sign-in throttling ─────────────────────────────────────────────

    /// <summary>Failures allowed before the delay starts growing.</summary>
    public int LoginFailuresBeforeDelay { get; init; } = 5;

    /// <summary>
    /// Base delay in seconds, doubled per failure past the threshold. Deliberately a
    /// delay and not a lockout: a lockout lets anyone who knows an address keep its
    /// owner permanently locked out, while a delay only penalises the attacker.
    /// </summary>
    public int LoginDelayBaseSeconds { get; init; } = 1;

    /// <summary>Ceiling on the delay, so a forgotten password is not a half-hour wait.</summary>
    public int LoginDelayMaxSeconds { get; init; } = 60;

    // ── Two-factor ────────────────────────────────────────────────────────────

    /// <summary>How long the post-password challenge stays completable.</summary>
    public int TwoFactorChallengeMinutes { get; init; } = 5;

    public int RecoveryCodeCount { get; init; } = 10;

    // ── Email change ──────────────────────────────────────────────────────────
    public int EmailChangeHours { get; init; } = 2;

    // ── Links inside emails ───────────────────────────────────────────────────
    public string FrontendBaseUrl { get; init; } = "http://localhost:4200";
}
