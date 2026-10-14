using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.WEB.Controllers;

/// <summary>Liveness and readiness for whatever is watching the service.</summary>
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(AppDbContext context, ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Reports healthy only if the database actually answers.
    ///
    /// <para>
    /// This used to return a hard-coded "online", so it kept reporting the service as
    /// fine while Postgres was down — the exact moment a health check exists to catch.
    /// A check that cannot fail tells you nothing.
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Ping(CancellationToken ct)
    {
        var timestamp = DateTime.UtcNow;

        try
        {
            // A real round trip, not just a pooled connection handle: CanConnect can
            // succeed against a server that is up but refusing to run queries.
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", ct);

            return Ok(new { status = "online", database = "up", timestamp });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed: the database did not respond.");

            // 503 rather than 500: the process is alive but not ready to serve, which
            // is the distinction a load balancer acts on.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "degraded",
                database = "down",
                timestamp
            });
        }
    }
}
