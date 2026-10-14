using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Controllers;

[Route("api/[controller]")]
[Authorize]
public class ContactController : ApiControllerBase
{
    private readonly IContactService _contact;

    public ContactController(IContactService contact) => _contact = contact;

    // POST /api/contact — body { "subject": "bug|feature|question|partnership|other", "message": "..." }
    // 204 · 400 VALIDATION_FAILED · 401 · 404 USER_NOT_FOUND
    // 429 AUTH_TOO_MANY_ATTEMPTS (3 per hour per user) · 500 CONTACT_UNAVAILABLE
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Contact)]
    public async Task<IActionResult> Send([FromBody] ContactRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _contact.SendAsync(userId.Value, request, ct);

        return result.Success ? NoContent() : Failure(result.Error!);
    }
}
