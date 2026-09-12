using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    // DELETE /api/user/me — 204 · 401 · 404
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _users.DeleteAccountAsync(userId.Value, ct);

        return result.Success ? NoContent() : Failure(result.Error!);
    }
}
