using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Controllers;

[Route("api/[controller]")]
[Authorize]
public class UserController : ApiControllerBase
{
    private readonly IUserService _users;

    public UserController(IUserService users) => _users = users;

    // GET /api/user/me — 200 · 401 AUTH_UNAUTHENTICATED · 404 USER_NOT_FOUND
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _users.GetMeAsync(userId.Value, ct);

        return result.Success ? Ok(result.Data) : Failure(result.Error!);
    }

    // PUT /api/user/me — 200 · 400 VALIDATION_FAILED · 401 · 404
    [HttpPut("me")]
    public async Task<IActionResult> Update([FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _users.UpdateAsync(userId.Value, request, ct);

        return result.Success ? Ok(result.Data) : Failure(result.Error!);
    }

    // DELETE /api/user/me — body { "password": "..." }
    // 204 · 400 AUTH_CURRENT_PASSWORD_INCORRECT | AUTH_PASSWORD_NOT_SET | VALIDATION_FAILED
    // 401 · 404 · 409 CONCURRENCY_CONFLICT · 429 AUTH_TOO_MANY_ATTEMPTS
    // Rate-limited like the other password-confirmed actions, so a stolen access token
    // cannot be used to guess the password here at full speed.
    [HttpDelete("me")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> DeleteAccount(
        [FromBody] PasswordConfirmationRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _users.DeleteAccountAsync(userId.Value, request.Password, ct);

        return result.Success ? NoContent() : Failure(result.Error!);
    }
}
