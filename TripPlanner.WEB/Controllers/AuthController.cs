using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Controllers;

[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ApiControllerBase
{
    private const string RefreshCookie = "refreshToken";

    private readonly IAuthService _auth;
    private readonly IWebHostEnvironment _env;

    public AuthController(IAuthService auth, IWebHostEnvironment env)
    {
        _auth = auth;
        _env = env;
    }

    // ── POST /api/auth/register ───────────────────────────────────────────────
    // 201 created · 409 AUTH_EMAIL_ALREADY_EXISTS · 400 AUTH_PASSWORD_TOO_WEAK
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Registration)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ct);

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
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await _auth.VerifyEmailAsync(request.Token, ct);

        return result.Success
            ? Ok(new { message = "Email confirmed. You can sign in now." })
            : Failure(result.Error!);
    }

    // ── POST /api/auth/resend-verification ────────────────────────────────────
    // Always 200 — revealing whether the address exists would leak account data.
    [HttpPost("resend-verification")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        await _auth.ResendVerificationAsync(request.Email, ct);

        return Ok(new { message = "If that address needs confirming, a new link is on its way." });
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────────
    // 200 · 401 AUTH_INVALID_CREDENTIALS · 403 AUTH_EMAIL_NOT_VERIFIED
    // 429 AUTH_TOO_MANY_ATTEMPTS
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, RequestContext, ct);

        if (!result.Success) return Failure(result.Error!);

        return Ok(IssueTokens(result.Data!));
    }

    // ── POST /api/auth/refresh ────────────────────────────────────────────────
    // 200 · 401 AUTH_REFRESH_TOKEN_MISSING | _INVALID | _EXPIRED | _REUSED
    //       | AUTH_SESSION_EXPIRED
    [HttpPost("refresh")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var cookie = Request.Cookies[RefreshCookie];

        var result = await _auth.RefreshAsync(cookie ?? string.Empty, RequestContext, ct);

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
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var cookie = Request.Cookies[RefreshCookie];

        ClearRefreshCookie();
        await _auth.LogoutAsync(cookie, ct);

        return Ok(new { message = "Signed out." });
    }

    // ── POST /api/auth/forgot-password ────────────────────────────────────────
    // Always 200, whether or not the address is registered.
    [HttpPost("forgot-password")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request.Email, ct);

        return Ok(new { message = "If an account exists for that address, a reset link is on its way." });
    }

    // ── POST /api/auth/reset-password ─────────────────────────────────────────
    // 200 · 400 TOKEN_INVALID | TOKEN_EXPIRED | TOKEN_ALREADY_USED
    //       | AUTH_PASSWORD_TOO_WEAK
    [HttpPost("reset-password")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _auth.ResetPasswordAsync(request.Token, request.NewPassword, ct);

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

        var result = await _auth.ChangePasswordAsync(
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

    private CookieOptions BuildCookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,

        // Dev runs over plain HTTP, where a Secure cookie would simply not be stored.
        Secure = !_env.IsDevelopment(),

        // Strict is safe here because the SPA calls the API directly rather than being
        // navigated to it from elsewhere.
        SameSite = SameSiteMode.Strict,

        // Scoped to the auth endpoints — no other request needs to carry it.
        Path = "/api/auth",

        Expires = expires
    };
}
