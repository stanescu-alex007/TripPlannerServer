using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Controllers
{
    [ApiController]
    [Route("api/trip/{tripId:guid}/schedule")]
    [Authorize]
    public class TripScheduleController : ControllerBase
    {
        private readonly ITripScheduleService _scheduleService;

        public TripScheduleController(ITripScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
        }

        // GET /api/trip/{tripId}/schedule
        [HttpGet]
        public async Task<IActionResult> GetSchedule(Guid tripId)
        {
            var items = await _scheduleService.GetScheduleAsync(tripId);
            return Ok(items);
        }

        // POST /api/trip/{tripId}/schedule
        [HttpPost]
        public async Task<IActionResult> AddItem(Guid tripId, [FromBody] CreateScheduleItemRequest request)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _scheduleService.AddItemAsync(tripId, userId.Value, request);
            if (!result.Success)
                return result.Message?.Contains("not found") == true
                    ? NotFound(new { message = result.Message })
                    : BadRequest(new { message = result.Message });

            return CreatedAtAction(nameof(GetSchedule), new { tripId }, result.Data);
        }

        // PUT /api/trip/{tripId}/schedule/{itemId}
        [HttpPut("{itemId:guid}")]
        public async Task<IActionResult> UpdateItem(
            Guid tripId, Guid itemId, [FromBody] UpdateScheduleItemRequest request)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _scheduleService.UpdateItemAsync(itemId, userId.Value, request);
            if (!result.Success)
                return result.Message?.Contains("not found") == true
                    ? NotFound(new { message = result.Message })
                    : BadRequest(new { message = result.Message });

            return Ok(result);
        }

        // PATCH /api/trip/{tripId}/schedule/{itemId}/status
        [HttpPatch("{itemId:guid}/status")]
        public async Task<IActionResult> UpdateStatus(
            Guid tripId, Guid itemId, [FromBody] UpdateScheduleItemStatusRequest request)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _scheduleService.UpdateStatusAsync(itemId, userId.Value, request.Status);
            if (!result.Success)
                return result.Message?.Contains("not found") == true
                    ? NotFound(new { message = result.Message })
                    : BadRequest(new { message = result.Message });

            return Ok(result);
        }

        // DELETE /api/trip/{tripId}/schedule/{itemId}
        [HttpDelete("{itemId:guid}")]
        public async Task<IActionResult> DeleteItem(Guid tripId, Guid itemId)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _scheduleService.DeleteItemAsync(itemId, userId.Value);
            if (!result.Success)
                return result.Message?.Contains("not found") == true
                    ? NotFound(new { message = result.Message })
                    : BadRequest(new { message = result.Message });

            return Ok(result);
        }

        private Guid? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
