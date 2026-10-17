using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;
using Microsoft.AspNetCore.RateLimiting;

namespace TripPlanner.WEB.Controllers;

[Route("api/[controller]")]
[Authorize]
public class TripController : ApiControllerBase
{
    private readonly ITripService _trips;
    private readonly ITripInvitationService _invitations;

    public TripController(ITripService trips, ITripInvitationService invitations)
    {
        _trips = trips;
        _invitations = invitations;
    }

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

    // POST /api/trip/{id}/cover — multipart/form-data, field "file"
    // 200 { image } · 400 IMAGE_MISSING | IMAGE_TOO_LARGE | IMAGE_INVALID
    // 403 TRIP_FORBIDDEN · 404 TRIP_NOT_FOUND · 429
    [HttpPost("{id:guid}/cover")]
    [EnableRateLimiting(RateLimitPolicies.Uploads)]
    [RequestSizeLimit(ImageUpload.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUpload.MaxRequestBytes)]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> UploadCover(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var (file, invalid) = await ReadUploadAsync(ct);
        if (invalid is not null) return Failure(invalid);

        await using var stream = file!.OpenReadStream();
        var result = await _trips.SetCoverAsync(id, userId.Value, stream, ct);

        return result.Success ? Ok(new { image = result.Data }) : Failure(result.Error!);
    }

    // DELETE /api/trip/{id}/cover — 204 · 403 TRIP_FORBIDDEN · 404 TRIP_NOT_FOUND
    [HttpDelete("{id:guid}/cover")]
    public async Task<IActionResult> RemoveCover(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _trips.RemoveCoverAsync(id, userId.Value, ct);

        return result.Success ? NoContent() : Failure(result.Error!);
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

    // GET /api/trip/deleted — trips the caller deleted, still restorable
    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeleted(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        return Ok(await _trips.GetDeletedTripsAsync(userId.Value, ct));
    }

    // POST /api/trip/{id}/restore — 200 · 404 TRIP_NOT_FOUND
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _trips.RestoreTripAsync(id, userId.Value, ct);

        return result.Success ? Ok(new { message = "Trip restored." }) : Failure(result.Error!);
    }

    // POST /api/trip/{id}/leave — 204 · 404 TRIP_NOT_FOUND | PARTICIPANT_NOT_FOUND
    // 409 TRIP_OWNER_CANNOT_LEAVE
    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _trips.LeaveTripAsync(id, userId.Value, ct);

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

    // POST /api/trip/{id}/participants/{participantId}/resend
    // 200 · 409 INVITATION_ALREADY_ANSWERED · 429 INVITATION_SEND_TOO_SOON
    [HttpPost("{id:guid}/participants/{participantId:guid}/resend")]
    [EnableRateLimiting(RateLimitPolicies.Sensitive)]
    public async Task<IActionResult> ResendInvitation(Guid id, Guid participantId, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null) return Failure(ErrorCodes.Unauthenticated);

        var result = await _invitations.ResendAsync(id, participantId, userId.Value, ct);

        return result.Success
            ? Ok(new { message = "Invitation sent again." })
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
