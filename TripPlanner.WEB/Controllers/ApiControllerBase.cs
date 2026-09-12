using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.WEB.Controllers;

/// <summary>
/// The single place where a domain <see cref="Error"/> becomes an HTTP response.
/// Controllers never pick status codes by hand and never branch on message text.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Shape of every error body: <c>{ "code": "...", "message": "..." }</c>.</summary>
    protected IActionResult Failure(Error error) =>
        StatusCode(StatusCodeFor(error.Type), new
        {
            code = error.Code,
            message = error.Message
        });

    protected static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError
    };

    /// <summary>The signed-in user's id, or null when the token is absent or malformed.</summary>
    protected Guid? CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    protected RequestContext RequestContext => new(
        IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent: Request.Headers.UserAgent.ToString() is { Length: > 0 } ua
            ? ua[..Math.Min(ua.Length, 512)]
            : null);
}
