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
/// <see cref="Code"/> is a stable contract — the frontend switches on it to pick
/// the message it shows the user. Never change an existing code's meaning; add a
/// new one instead. <see cref="Message"/> is a human-readable fallback only.
/// </para>
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type);
