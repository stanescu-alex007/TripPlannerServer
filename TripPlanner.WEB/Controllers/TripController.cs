using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TripController : ControllerBase
    {
        private readonly ITripService _tripService;

        public TripController(ITripService tripService)
        {
            _tripService = tripService;
        }

        // ── GET /api/trip ──────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetMyTrips()
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var trips = await _tripService.GetMyTripsAsync(userId.Value);
            return Ok(trips);
        }

        // ── GET /api/trip/{id} ─────────────────────────────────────────────
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var trip = await _tripService.GetTripByIdAsync(id);
            if (trip is null) return NotFound(new { message = "Trip not found." });
            return Ok(trip);
        }

        // ── POST /api/trip ─────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTripRequest request)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var trip = await _tripService.CreateTripAsync(userId.Value, request);
            return CreatedAtAction(nameof(GetById), new { id = trip.Id }, trip);
        }

        // ── PUT /api/trip/{id} ─────────────────────────────────────────────
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTripRequest request)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _tripService.UpdateTripAsync(id, userId.Value, request);
            if (!result.Success) return result.Message == "Trip not found." ? NotFound(result) : Forbid();

            return Ok(result);
        }

        // ── DELETE /api/trip/{id} ──────────────────────────────────────────
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _tripService.DeleteTripAsync(id, userId.Value);
            if (!result.Success) return result.Message == "Trip not found." ? NotFound(result) : Forbid();

            return Ok(result);
        }

        // ── POST /api/trip/{id}/participants ───────────────────────────────
        [HttpPost("{id:guid}/participants")]
        public async Task<IActionResult> AddParticipant(Guid id, [FromBody] AddParticipantRequest request)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _tripService.AddParticipantAsync(id, userId.Value, request);
            if (!result.Success) return BadRequest(result);

            return Ok(result);
        }

        // ── DELETE /api/trip/{id}/participants/{participantId} ─────────────
        [HttpDelete("{id:guid}/participants/{participantId:guid}")]
        public async Task<IActionResult> RemoveParticipant(Guid id, Guid participantId)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _tripService.RemoveParticipantAsync(id, userId.Value, participantId);
            if (!result.Success) return BadRequest(result);

            return Ok(result);
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
