using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Controllers;

[Route("api/[controller]")]
public class AuthController : ApiControllerBase
{
    // Deliberately no class-level [AllowAnonymous]: it wins over a method-level
    // [Authorize], which would leave change-password unprotected by the framework
    // and relying purely on its own CurrentUserId check. Each anonymous action opts
    // in for itself instead.
    private const string RefreshCookie = "refreshToken";

    private readonly IRegistrationService _registration;
    private readonly ISignInService _signIn;
    private readonly ISessionService _sessions;
    private readonly IPasswordManagementService _passwords;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public AuthController(
        IRegistrationService registration,
        ISignInService signIn,
        ISessionService sessions,
        IPasswordManagementService passwords,
        IWebHostEnvironment env,
        IConfiguration config)
    {
        _registration = registration;
        _signIn = signIn;
        _sessions = sessions;
        _passwords = passwords;
        _env = env;
        _config = config;
    }

    // ── POST /api/auth/register ───────────────────────────────────────────────
    // 201 created · 409 AUTH_EMAIL_ALREADY_EXISTS · 400 AUTH_PASSWORD_TOO_WEAK
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Registration)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _registration.RegisterAsync(request, ct);

        if (!result.Success) return Failure(result.Error!);

        return StatusCode(StatusCodes.Status201Created, new
        {
            message = "Account created. Check your inbox to confirm your email address."
        });
    }

    // ── POST /api/auth/verify-email ───────────────────────────────────────────
    // 200 · 400 TOKEN_INVALID | TOKEN_EXPIRED | TOKEN_ALREADY_USED
    // 409 AUTH_EMAIL_ALREADY_VERIFIED
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await _registration.VerifyEmailAsync(request.Token, ct);

        return result.Success
            ? Ok(new { message = "Email confirmed. You can sign in now." })
            : Failure(result.Error!);
    }

    // ── POST /api/auth/resend-verification ────────────────────────────────────
    // Always 200 — revealing whether the address exists would leak account data.
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        await _registration.ResendVerificationAsync(request.Email, ct);

        return Ok(new { message = "If that address needs confirming, a new link is on its way." });
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────
    // 200 tokens, or 200 {requiresTwoFactor, challengeToken} when 2FA is on
    // 401 AUTH_INVALID_CREDENTIALS · 403 AUTH_EMAIL_NOT_VERIFIED
    // 429 AUTH_TOO_MANY_ATTEMPTS | AUTH_LOGIN_THROTTLED
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _signIn.LoginAsync(request, RequestContext, ct);

        if (!result.Success) return Failure(result.Error!);

        var outcome = result.Data!;

        // A pending second factor is a step, not a failure — 200 with what to do next,
        // so the client is not left guessing from an error code.
        if (outcome.RequiresTwoFactor)
        {
            return Ok(new
            {
                requiresTwoFactor = true,
                challengeToken = outcome.ChallengeToken
            });
        }

        return Ok(IssueTokens(outcome.Tokens!));
    }

    // ── POST /api/auth/2fa ────────────────────────────────────────────────────
    // Completes a sign-in with an authenticator code or a recovery code.
    // 200 · 400 AUTH_2FA_CODE_INVALID · 401 AUTH_2FA_CHALLENGE_INVALID
    // 429 AUTH_LOGIN_THROTTLED
    [HttpPost("2fa")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> CompleteTwoFactor(
        [FromBody] TwoFactorLoginRequest request, CancellationToken ct)
    {
        var result = await _signIn.CompleteTwoFactorAsync(
            request.ChallengeToken, request.Code, RequestContext, ct);

        if (!result.Success) return Failure(result.Error!);

        return Ok(IssueTokens(result.Data!));
    }

    // ── POST /api/auth/refresh ────────────────────────────────────────────────
    // 200 · 401 AUTH_REFRESH_TOKEN_MISSING | _INVALID | _EXPIRED | _REUSED
    //       | AUTH_SESSION_EXPIRED
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Refresh)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var cookie = Request.Cookies[RefreshCookie];

        var result = await _sessions.RefreshAsync(cookie ?? string.Empty, RequestContext, ct);

        if (!result.Success)
        {
            // The cookie is worthless now — clear it so the browser stops replaying it.
            ClearRefreshCookie();
            return Failure(result.Error!);
        }

        return Ok(IssueTokens(result.Data!));
    }

    // ── POST /api/auth/logout ─────────────────────────────────────────────────
    // Always 200. Anonymous on purpose: identity is taken from the refresh cookie, so
    // signing out still works once the access token has expired.
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var cookie = Request.Cookies[RefreshCookie];

        ClearRefreshCookie();
        await _sessions.LogoutAsync(cookie, ct);

        return Ok(new { message = "Signed out." });
    }

    // ── POST /api/auth/forgot-password ────────────────────────────────────────
    // Always 200, whether or not the address is registered.
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _passwords.ForgotPasswordAsync(request.Email, ct);

        return Ok(new { message = "If an account exists for that address, a reset link is on its way." });
    }

    // ── POST /api/auth/reset-password ─────────────────────────────────────────
    // 200 · 400 TOKEN_INVALID | TOKEN_EXPIRED | TOKEN_ALREADY_USED
    //       | AUTH_PASSWORD_TOO_WEAK
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _passwords.ResetPasswordAsync(request.Token, request.NewPassword, ct);

        if (!result.Success) return Failure(result.Error!);

        // Every session died with the reset, this one included.
        ClearRefreshCookie();

        return Ok(new { message = "Password updated. Please sign in with your new password." });
    }

    // ── POST /api/auth/change-password ────────────────────────────────────────
    // 200 · 400 AUTH_CURRENT_PASSWORD_INCORRECT | AUTH_PASSWORD_TOO_WEAK
    //       | AUTH_PASSWORD_SAME_AS_CURRENT
    // 401 AUTH_UNAUTHENTICATED
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _passwords.ChangePasswordAsync(
            userId.Value, request.CurrentPassword, request.NewPassword, RequestContext, ct);

        if (!result.Success) return Failure(result.Error!);

        // Other devices were signed out; this one gets a fresh pair.
        return Ok(IssueTokens(result.Data!));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Puts the refresh token in an HttpOnly cookie and returns only the access token in
    /// the body — the refresh token must never be readable from JavaScript.
    /// </summary>
    private object IssueTokens(AuthTokens tokens)
    {
        Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, BuildCookieOptions(tokens.RefreshTokenExpiresAt));

        return new
        {
            accessToken = tokens.AccessToken,
            expiresAt = tokens.AccessTokenExpiresAt
        };
    }

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookie, BuildCookieOptions(DateTime.UtcNow.AddDays(-1)));

    private CookieOptions BuildCookieOptions(DateTimeOffset expires)
    {
        // Strict works while the SPA and the API share a site. Once they live on
        // different domains the browser stops sending the cookie altogether and refresh
        // silently dies — which is why this is configuration, not a constant.
        // Cross-site ("None") is only honoured on HTTPS.
        var configured = _config["Cookies:SameSite"];

        var sameSite = configured?.ToLowerInvariant() switch
        {
            "none" => SameSiteMode.None,
            "lax" => SameSiteMode.Lax,
            _ => SameSiteMode.Strict
        };

        return new CookieOptions
        {
            HttpOnly = true,

            // Dev runs over plain HTTP, where a Secure cookie would not be stored at
            // all. SameSite=None additionally requires it.
            Secure = !_env.IsDevelopment() || sameSite == SameSiteMode.None,

            SameSite = sameSite,

            // Scoped to the auth endpoints — no other request needs to carry it.
            Path = "/api/auth",

            Expires = expires
        };
    }
}
