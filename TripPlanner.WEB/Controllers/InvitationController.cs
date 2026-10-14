using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.WEB.Controllers;

/// <summary>
/// Responding to trip invitations, both from the emailed link and from inside the app.
/// </summary>
[Route("api/invitations")]
public class InvitationController : ApiControllerBase
{
    private readonly ITripInvitationService _invitations;

    public InvitationController(ITripInvitationService invitations) => _invitations = invitations;

    // ── GET /api/invitations/{token} ──────────────────────────────────────────
    // Anonymous on purpose: the link is opened from an inbox, usually in a browser
    // that is not signed in. It returns only what the email already said.
    // 200 · 404 INVITATION_NOT_FOUND
    [HttpGet("{token}")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> Preview(string token, CancellationToken ct)
    {
        var result = await _invitations.PreviewAsync(token, ct);

        return result.Success ? Ok(result.Data) : Failure(result.Error!);
    }

    // ── POST /api/invitations/{token}/accept ──────────────────────────────────
    // Requires sign-in: accepting puts an account on the trip, so there has to be
    // an account, and it has to be the invited one.
    // 200 · 401 · 403 INVITATION_EMAIL_MISMATCH · 409 INVITATION_ALREADY_ANSWERED
    [HttpPost("{token}/accept")]
    [Authorize]
    public async Task<IActionResult> Accept(string token, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _invitations.AcceptByTokenAsync(token, userId.Value, ct);

        return result.Success
            ? Ok(new { message = "You're on the trip 🎉" })
            : Failure(result.Error!);
    }

    // ── POST /api/invitations/{token}/decline ─────────────────────────────────
    // Anonymous: making someone register in order to say "no" means most invitations
    // simply never get answered. Holding the token is proof enough to refuse.
    [HttpPost("{token}/decline")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> Decline(string token, CancellationToken ct)
    {
        var result = await _invitations.DeclineByTokenAsync(token, ct);

        return result.Success
            ? Ok(new { message = "Invitation declined." })
            : Failure(result.Error!);
    }

    // ── GET /api/invitations/mine ─────────────────────────────────────────────
    // So an invitation is still findable after the email is lost.
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        return Ok(await _invitations.GetPendingForUserAsync(userId.Value, ct));
    }

    // ── POST /api/invitations/trip/{tripId}/accept|decline ────────────────────
    // The in-app path: no token needed, the invitation is matched by the signed-in
    // user's own address.
    [HttpPost("trip/{tripId:guid}/accept")]
    [Authorize]
    public Task<IActionResult> AcceptInApp(Guid tripId, CancellationToken ct) =>
        RespondInApp(tripId, accept: true, ct);

    [HttpPost("trip/{tripId:guid}/decline")]
    [Authorize]
    public Task<IActionResult> DeclineInApp(Guid tripId, CancellationToken ct) =>
        RespondInApp(tripId, accept: false, ct);

    private async Task<IActionResult> RespondInApp(Guid tripId, bool accept, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _invitations.RespondInAppAsync(tripId, userId.Value, accept, ct);

        if (!result.Success) return Failure(result.Error!);

        return Ok(new { message = accept ? "You're on the trip 🎉" : "Invitation declined." });
    }
}
