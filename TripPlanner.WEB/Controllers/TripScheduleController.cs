using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Controllers;

[Route("api/trip/{tripId:guid}/schedule")]
[Authorize]
public class TripScheduleController : ApiControllerBase
{
    private readonly ITripScheduleService _schedule;

    public TripScheduleController(ITripScheduleService schedule) => _schedule = schedule;

    // GET — 200 · 403 TRIP_FORBIDDEN · 404 TRIP_NOT_FOUND
    [HttpGet]
    public async Task<IActionResult> GetSchedule(Guid tripId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _schedule.GetScheduleAsync(tripId, userId.Value, ct);

        return result.Success ? Ok(result.Data) : Failure(result.Error!);
    }

    // POST — 201 · 400 SCHEDULE_DATE_OUT_OF_RANGE · 403 · 404
    [HttpPost]
    public async Task<IActionResult> AddItem(
        Guid tripId, [FromBody] CreateScheduleItemRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _schedule.AddItemAsync(tripId, userId.Value, request, ct);

        if (!result.Success) return Failure(result.Error!);

        return CreatedAtAction(nameof(GetSchedule), new { tripId }, result.Data);
    }

    // PUT — 200 · 400 · 403 · 404 SCHEDULE_ITEM_NOT_FOUND
    [HttpPut("{itemId:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid tripId, Guid itemId, [FromBody] UpdateScheduleItemRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _schedule.UpdateItemAsync(tripId, itemId, userId.Value, request, ct);

        return result.Success ? Ok(new { message = "Activity updated." }) : Failure(result.Error!);
    }

    // PATCH — 200 · 400 SCHEDULE_STATUS_INVALID · 403 · 404
    [HttpPatch("{itemId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid tripId, Guid itemId, [FromBody] UpdateScheduleItemStatusRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _schedule.UpdateStatusAsync(tripId, itemId, userId.Value, request.Status, ct);

        return result.Success ? Ok(new { message = "Status updated." }) : Failure(result.Error!);
    }

    // DELETE — 204 · 403 · 404
    [HttpDelete("{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid tripId, Guid itemId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _schedule.DeleteItemAsync(tripId, itemId, userId.Value, ct);

        return result.Success ? NoContent() : Failure(result.Error!);
    }
}
