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

    /// <summary>
    /// Reads the "file" field of a multipart upload, checking only presence and size —
    /// whether it is really an image is decided by decoding it, never by its name or
    /// Content-Type. Read by hand rather than bound as a parameter: binding turns a body
    /// over the limit into a generic VALIDATION_FAILED instead of IMAGE_TOO_LARGE.
    /// </summary>
    protected async Task<(IFormFile? File, Error? Error)> ReadUploadAsync(CancellationToken ct)
    {
        if (!Request.HasFormContentType) return (null, ErrorCodes.ImageMissing);

        IFormCollection form;

        try
        {
            form = await Request.ReadFormAsync(ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return (null, ErrorCodes.ImageTooLarge);
        }
        catch (Exception ex) when (ex is BadHttpRequestException or InvalidDataException)
        {
            return (null, ErrorCodes.ValidationFailed);
        }

        return form.Files.GetFile("file") switch
        {
            null or { Length: 0 } => (null, ErrorCodes.ImageMissing),
            { Length: > ImageUpload.MaxFileBytes } => (null, ErrorCodes.ImageTooLarge),
            var file => (file, null)
        };
    }

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

    /// <summary>
    /// Which session the caller is using, from the access token's "sid" claim. Lets a
    /// request recognise its own session without the refresh cookie, which is scoped to
    /// /api/auth and never reaches these endpoints.
    /// </summary>
    protected Guid? CurrentSessionId =>
        Guid.TryParse(User.FindFirst("sid")?.Value, out var id) ? id : null;

    protected RequestContext RequestContext => new(
        IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
        UserAgent: Request.Headers.UserAgent.ToString() is { Length: > 0 } ua
            ? ua[..Math.Min(ua.Length, 512)]
            : null);
}
