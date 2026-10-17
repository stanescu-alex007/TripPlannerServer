using TripPlanner.Core.Common;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB.Middleware;

/// <summary>
/// Last line of defence: converts any unhandled exception into the standard
/// <c>{ code, message }</c> body. Without it, an unexpected failure leaks a .NET stack
/// trace straight to the client.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client hung up — not an error worth reporting or logging as one.
        }
        catch (ConcurrencyConflictException ex) when (!context.Response.HasStarted)
        {
            // Services translate the conflicts they expect into a Result; this is the
            // safety net for any path that does not. A lost race is the caller's cue to
            // reload and retry, not a server fault, so it is a 409 rather than a 500.
            _logger.LogWarning(ex,
                "Unhandled concurrency conflict for {Method} {Path}.",
                context.Request.Method, context.Request.Path);

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(
                ApiResponse.Failure(StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception for {Method} {Path}.",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw; // too late to rewrite the response

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            // No exception details in the body, in any environment: the full exception
            // is in the log line above, and errors always carry data = null.
            await context.Response.WriteAsJsonAsync(
                ApiResponse.Failure(StatusCodes.Status500InternalServerError, ErrorCodes.Internal));
        }
    }
}
