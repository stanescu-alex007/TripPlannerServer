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
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        // ── GET /api/user/me ───────────────────────────────────────────────
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var user = await _userService.GetMeAsync(userId.Value);
            if (user is null) return NotFound();

            return Ok(user);
        }

        // ── PUT /api/user/update ───────────────────────────────────────────
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateUserRequest request)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            var result = await _userService.UpdateAsync(userId.Value, request);
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
