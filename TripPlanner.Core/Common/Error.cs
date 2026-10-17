namespace TripPlanner.Core.Common;

/// <summary>
/// Transport-agnostic error category. The WEB layer maps this to an HTTP status
/// code, so Core never has to know about HTTP.
/// </summary>
public enum ErrorType
{
    /// <summary>Input the caller can fix → 400</summary>
    Validation,

    /// <summary>Caller is not (or no longer) authenticated → 401</summary>
    Unauthenticated,

    /// <summary>Authenticated, but not allowed to do this → 403</summary>
    Forbidden,

    /// <summary>Target does not exist → 404</summary>
    NotFound,

    /// <summary>Conflicts with current state → 409</summary>
    Conflict,

    /// <summary>Rate limited → 429</summary>
    TooManyRequests,

    /// <summary>Unexpected server-side failure → 500</summary>
    Internal
}

/// <summary>
/// A failure the client can react to programmatically.
/// <para>
/// <see cref="Code"/> is a stable contract. Never change an existing code's meaning;
/// add a new one instead. It reaches clients as <see cref="MessageKey"/>, derived from
/// it, so the two can never drift. <see cref="Message"/> is for logs and readers of
/// this code only — clients get the key and supply their own wording.
/// </para>
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>
    /// Translation key sent to clients: the first segment of the code becomes the
    /// namespace and the rest is camel-cased, e.g. <c>AUTH_INVALID_CREDENTIALS</c> →
    /// <c>errors.auth.invalidCredentials</c>, <c>AUTH_2FA_CODE_INVALID</c> →
    /// <c>errors.auth.2faCodeInvalid</c>. Mechanical on purpose, so a client can map
    /// a key back to its code.
    /// </summary>
    public string MessageKey
    {
        get
        {
            var parts = Code.ToLowerInvariant().Split('_', StringSplitOptions.RemoveEmptyEntries);
            var rest = string.Concat(parts.Skip(1).Select((p, i) =>
                i == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));

            return rest.Length == 0 ? $"errors.{parts[0]}" : $"errors.{parts[0]}.{rest}";
        }
    }
}
