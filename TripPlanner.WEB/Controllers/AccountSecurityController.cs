using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Controllers;

/// <summary>
/// Account-security settings for a signed-in user: two-factor, recovery codes,
/// email address and active sessions.
/// </summary>
[Route("api/account")]
[Authorize]
public class AccountSecurityController : ApiControllerBase
{
    private const string RefreshCookie = "refreshToken";

    private readonly IAccountSecurityService _security;

    public AccountSecurityController(IAccountSecurityService security) => _security = security;

    // ── TWO-FACTOR ────────────────────────────────────────────────────────────

    // POST /api/account/2fa/setup — 200 {secret, otpAuthUri} · 409 AUTH_2FA_ALREADY_ENABLED
    [HttpPost("2fa/setup")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> BeginTwoFactorSetup(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _security.BeginTwoFactorSetupAsync(userId.Value, ct);

        return result.Success ? Ok(result.Data) : Failure(result.Error!);
    }

    // POST /api/account/2fa/enable — 200 {codes} · 400 AUTH_2FA_CODE_INVALID
    [HttpPost("2fa/enable")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> EnableTwoFactor(
        [FromBody] EnableTwoFactorRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _security.EnableTwoFactorAsync(
            userId.Value, request.Secret, request.Code, ct);

        return result.Success ? Ok(result.Data) : Failure(result.Error!);
    }

    // POST /api/account/2fa/disable — 200 · 400 AUTH_CURRENT_PASSWORD_INCORRECT
    [HttpPost("2fa/disable")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> DisableTwoFactor(
        [FromBody] PasswordConfirmationRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _security.DisableTwoFactorAsync(userId.Value, request.Password, ct);

        return result.Success
            ? Ok(new { message = "Two-factor authentication is off." })
            : Failure(result.Error!);
    }

    // POST /api/account/2fa/recovery-codes — replaces the previous set
    [HttpPost("2fa/recovery-codes")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> RegenerateRecoveryCodes(
        [FromBody] PasswordConfirmationRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _security.RegenerateRecoveryCodesAsync(userId.Value, request.Password, ct);

        return result.Success ? Ok(result.Data) : Failure(result.Error!);
    }

    // ── EMAIL CHANGE ──────────────────────────────────────────────────────────

    // POST /api/account/email-change — 202 · 409 AUTH_EMAIL_ALREADY_EXISTS
    [HttpPost("email-change")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> RequestEmailChange(
        [FromBody] RequestEmailChangeRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _security.RequestEmailChangeAsync(
            userId.Value, request.NewEmail, request.Password, ct);

        if (!result.Success) return Failure(result.Error!);

        return Accepted(new
        {
            message = "Check the new address for a confirmation link. Nothing changes until you confirm it."
        });
    }

    // POST /api/account/email-change/confirm
    // Anonymous: the link is followed from an inbox, quite possibly in a browser that
    // is not signed in. The token itself is the proof.
    [HttpPost("email-change/confirm")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ConfirmEmailChange(
        [FromBody] ConfirmEmailChangeRequest request, CancellationToken ct)
    {
        var result = await _security.ConfirmEmailChangeAsync(request.Token, ct);

        if (!result.Success) return Failure(result.Error!);

        // Every session died with the change, so the cookie here is worthless now.
        Response.Cookies.Delete(RefreshCookie, new CookieOptions { Path = "/api/auth" });

        return Ok(new { message = "Email address updated. Please sign in again." });
    }

    // ── SESSIONS ──────────────────────────────────────────────────────────────

    // GET /api/account/sessions
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _security.GetSessionsAsync(userId.Value, CurrentSessionId, ct);

        return result.Success ? Ok(result.Data) : Failure(result.Error!);
    }

    // DELETE /api/account/sessions/{id} — 204 · 404 SESSION_NOT_FOUND
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(Guid sessionId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _security.RevokeSessionAsync(userId.Value, sessionId, ct);

        return result.Success ? NoContent() : Failure(result.Error!);
    }

    // DELETE /api/account/sessions — sign out everywhere but here
    [HttpDelete("sessions")]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _security.RevokeOtherSessionsAsync(userId.Value, CurrentSessionId, ct);

        return result.Success
            ? Ok(new { revoked = result.Data })
            : Failure(result.Error!);
    }
}
