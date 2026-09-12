using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TripPlanner.Core.Common;

namespace TripPlanner.WEB;

public static class ApiValidation
{
    /// <summary>
    /// Renders DataAnnotations failures in the same envelope as domain errors, plus a
    /// per-field breakdown the frontend can attach to inputs:
    /// <code>
    /// { "code": "VALIDATION_FAILED", "message": "...", "fields": { "email": ["..."] } }
    /// </code>
    /// </summary>
    public static IActionResult BuildResponse(ActionContext context)
    {
        var fields = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => ToCamelCase(entry.Key),
                entry => entry.Value!.Errors
                    .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage)
                        ? "Invalid value."
                        : e.ErrorMessage)
                    .ToArray());

        return new BadRequestObjectResult(new
        {
            code = ErrorCodes.ValidationFailed.Code,
            message = ErrorCodes.ValidationFailed.Message,
            fields
        });
    }

    /// <summary>
    /// ModelState keys keep the C# casing ("Email"), while response bodies are camelCase —
    /// so the key has to be converted for the client to match it to a field.
    /// </summary>
    private static string ToCamelCase(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        var segments = key.Split('.');

        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i].Length > 0 && char.IsUpper(segments[i][0]))
                segments[i] = char.ToLowerInvariant(segments[i][0]) + segments[i][1..];
        }

        return string.Join('.', segments);
    }
}
