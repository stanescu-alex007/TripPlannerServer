using TripPlanner.Core.Common;

namespace TripPlanner.Core.Models
{
    /// <summary>
    /// Outcome of an operation. On failure it always carries a structured
    /// <see cref="Common.Error"/> — controllers map <see cref="Common.Error.Type"/>
    /// to an HTTP status instead of string-matching on messages.
    /// </summary>
    public class Result
    {
        public bool Success { get; init; }
        public Error? Error { get; init; }

        /// <summary>Human-readable message, for logging and as a client fallback.</summary>
        public string? Message => Error?.Message;

        public static Result Ok() => new() { Success = true };

        public static Result Fail(Error error) => new() { Success = false, Error = error };
    }

    public class Result<T>
    {
        public bool Success { get; init; }
        public Error? Error { get; init; }
        public T? Data { get; init; }

        public string? Message => Error?.Message;

        public static Result<T> Ok(T data) => new() { Success = true, Data = data };

        public static Result<T> Fail(Error error) => new() { Success = false, Error = error };
    }
}
