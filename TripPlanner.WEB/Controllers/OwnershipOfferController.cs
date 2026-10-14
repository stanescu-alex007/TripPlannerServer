using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.WEB.Controllers;

/// <summary>
/// Offers to take over a trip whose owner deleted their account. Always answered by a
/// signed-in user: accepting makes them the owner, so there is no anonymous link.
/// </summary>
[Route("api/ownership-offers")]
[Authorize]
public class OwnershipOfferController : ApiControllerBase
{
    private readonly ITripOwnershipService _ownership;

    public OwnershipOfferController(ITripOwnershipService ownership) => _ownership = ownership;

    // GET /api/ownership-offers — offers waiting on the caller
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        return Ok(await _ownership.GetOffersForUserAsync(userId.Value, ct));
    }

    // POST /api/ownership-offers/{id}/accept
    // 200 · 400 OWNERSHIP_OFFER_EXPIRED · 404 OWNERSHIP_OFFER_NOT_FOUND
    // 409 OWNERSHIP_OFFER_ALREADY_ANSWERED
    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _ownership.AcceptAsync(id, userId.Value, ct);

        return result.Success
            ? Ok(new { message = "You now own this trip." })
            : Failure(result.Error!);
    }

    // POST /api/ownership-offers/{id}/decline — same codes as accept
    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _ownership.DeclineAsync(id, userId.Value, ct);

        return result.Success
            ? Ok(new { message = "Offer declined." })
            : Failure(result.Error!);
    }
}
