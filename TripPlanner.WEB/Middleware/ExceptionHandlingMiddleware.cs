using TripPlanner.Core.Common;

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

            await context.Response.WriteAsJsonAsync(new
            {
                code = ErrorCodes.Internal.Code,
                message = ErrorCodes.Internal.Message,

                // Details only in development; production callers get nothing exploitable.
                detail = _env.IsDevelopment() ? ex.ToString() : null
            });
        }
    }
}
