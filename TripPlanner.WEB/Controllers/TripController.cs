using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Controllers;

[Route("api/[controller]")]
[Authorize]
public class TripController : ApiControllerBase
{
    private readonly ITripService _trips;

    public TripController(ITripService trips) => _trips = trips;

    // GET /api/trip — trips owned by, or shared with, the caller
    [HttpGet]
    public async Task<IActionResult> GetMyTrips(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        return Ok(await _trips.GetMyTripsAsync(userId.Value, ct));
    }

    // GET /api/trip/{id} — 200 · 404 TRIP_NOT_FOUND (also when the caller has no access)
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _trips.GetTripByIdAsync(id, userId.Value, ct);

        return result.Success ? Ok(result.Data) : Failure(result.Error!);
    }

    // POST /api/trip — 201 · 400 TRIP_DATE_RANGE_INVALID | VALIDATION_FAILED
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTripRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _trips.CreateTripAsync(userId.Value, request, ct);

        if (!result.Success) return Failure(result.Error!);

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    // PUT /api/trip/{id} — 200 · 403 TRIP_FORBIDDEN · 404 TRIP_NOT_FOUND
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTripRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _trips.UpdateTripAsync(id, userId.Value, request, ct);

        return result.Success ? Ok(new { message = "Trip updated." }) : Failure(result.Error!);
    }

    // DELETE /api/trip/{id} — 204 · 403 TRIP_FORBIDDEN · 404 TRIP_NOT_FOUND
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _trips.DeleteTripAsync(id, userId.Value, ct);

        return result.Success ? NoContent() : Failure(result.Error!);
    }

    // POST /api/trip/{id}/participants — 201 · 409 PARTICIPANT_ALREADY_EXISTS
    [HttpPost("{id:guid}/participants")]
    public async Task<IActionResult> AddParticipant(
        Guid id, [FromBody] AddParticipantRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _trips.AddParticipantAsync(id, userId.Value, request, ct);

        return result.Success
            ? StatusCode(StatusCodes.Status201Created, new { message = "Participant added." })
            : Failure(result.Error!);
    }

    // DELETE /api/trip/{id}/participants/{participantId}
    // 204 · 400 PARTICIPANT_OWNER_IMMUTABLE · 404 PARTICIPANT_NOT_FOUND
    [HttpDelete("{id:guid}/participants/{participantId:guid}")]
    public async Task<IActionResult> RemoveParticipant(Guid id, Guid participantId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _trips.RemoveParticipantAsync(id, userId.Value, participantId, ct);

        return result.Success ? NoContent() : Failure(result.Error!);
    }
}
