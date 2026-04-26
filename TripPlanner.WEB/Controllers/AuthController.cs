namespace TripPlanner.WEB.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using System.Security.Claims;
    using TripPlanner.Core.Interfaces.IServices;
    using TripPlanner.Core.Models;

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IWebHostEnvironment _env;

        public AuthController(IAuthService authService, IWebHostEnvironment env)
        {
            _authService = authService;
            _env = env;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var result = await _authService.RegisterAsync(request);

            if (!result.Success)
                return BadRequest(new { success = false, message = result.Message });

            return Ok(new { success = true, message = "Account created successfully." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var result = await _authService.LoginAsync(request);

            if (!result.Success)
                return Unauthorized(new { success = false, message = result.Message ?? "Invalid credentials." });

            SetRefreshTokenCookie(result.RefreshToken!);

            return Ok(new
            {
                success = true,
                accessToken = result.AccessToken
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var refreshToken = Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized(new { success = false, message = "No refresh token." });

            var result = await _authService.RefreshAsync(refreshToken);

            if (!result.Success)
                return Unauthorized(new { success = false, message = "Session expired." });

            SetRefreshTokenCookie(result.RefreshToken!);

            return Ok(new
            {
                success = true,
                accessToken = result.AccessToken
            });
        }

        // Logout does NOT require [Authorize] so it works even if the access token
        // has already expired — the refresh cookie is enough to identify and revoke.
        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];

            // Always delete the cookie regardless of whether revocation succeeds
            Response.Cookies.Delete("refreshToken");

            if (!string.IsNullOrEmpty(refreshToken))
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(userIdClaim, out var userId))
                {
                    await _authService.RevokeAsync(userId, refreshToken);
                }
            }

            return Ok(new { success = true });
        }

        private void SetRefreshTokenCookie(string token)
        {
            Response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                // In development the backend typically runs on HTTP, so Secure must be false
                // to allow the browser to store the cookie. In production always use HTTPS.
                Secure = !_env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });
        }
    }

}
