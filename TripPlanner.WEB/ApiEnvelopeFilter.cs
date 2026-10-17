using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TripPlanner.Core.Models;

namespace TripPlanner.WEB;

/// <summary>
/// Wraps every controller result in <see cref="ApiResponse"/>, so actions keep
/// returning <c>Ok(dto)</c> or <c>NoContent()</c> and the envelope is applied in one
/// place. Responses written outside MVC — authentication challenges, rate limiting,
/// the exception middleware — build the envelope themselves.
/// </summary>
public sealed class ApiEnvelopeFilter : IAsyncAlwaysRunResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        context.Result = context.Result switch
        {
            // Already enveloped (Failure(), validation) or a download: leave it alone.
            ObjectResult { Value: ApiResponse } or FileResult => context.Result,

            ObjectResult obj => Envelope(obj.StatusCode ?? StatusCodes.Status200OK, obj.Value),

            // A body-less success such as NoContent() becomes 200 with data = null: a
            // 204 may not carry a body, and every response is meant to have one.
            StatusCodeResult { StatusCode: < 400 } or EmptyResult =>
                Envelope(StatusCodes.Status200OK, null),

            StatusCodeResult status => Envelope(status.StatusCode, null),

            var other => other
        };

        return next();
    }

    private static ObjectResult Envelope(int status, object? value)
    {
        // A failure here came from the framework (ProblemDetails for an unknown route,
        // an unsupported content type...), never from an Error, so it is keyed by status.
        var body = status >= 400
            ? ApiResponse.Failure(status, ApiResponse.MessageKeyForStatus(status))
            : ApiResponse.Success(status, value);

        return new ObjectResult(body) { StatusCode = status };
    }
}
