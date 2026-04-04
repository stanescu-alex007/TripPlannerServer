namespace TripPlanner.WEB.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;
    using TripPlanner.Core.Interfaces.IServices;
    using TripPlanner.Core.Models;

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);

            if (!result.Success)
                return BadRequest(result.Message);

            return Ok();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);

            if (!result.Success)
                return Unauthorized(result.Message);

            SetRefreshTokenCookie(result.RefreshToken);

            return Ok(new
            {
                accessToken = result.AccessToken
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var refreshToken = Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized();

            var result = await _authService.RefreshAsync(refreshToken);

            if (!result.Success)
                return Unauthorized();

            SetRefreshTokenCookie(result.RefreshToken);

            return Ok(new
            {
                accessToken = result.AccessToken
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var refreshToken = Request.Cookies["refreshToken"];

            if (userId == null || refreshToken == null)
                return Unauthorized();

            await _authService.RevokeAsync(Guid.Parse(userId), refreshToken);

            Response.Cookies.Delete("refreshToken");

            return Ok();
        }

        private void SetRefreshTokenCookie(string token)
        {
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });
        }
    }

}
