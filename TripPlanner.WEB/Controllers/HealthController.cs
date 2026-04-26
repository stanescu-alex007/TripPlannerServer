namespace TripPlanner.WEB.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/health")]
    [AllowAnonymous]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Ping() => Ok(new { status = "online", timestamp = DateTime.UtcNow });
    }
}
