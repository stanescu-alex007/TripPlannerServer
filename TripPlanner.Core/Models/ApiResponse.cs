using System.Text.Json.Serialization;
using TripPlanner.Core.Common;

namespace TripPlanner.Core.Models
{
    /// <summary>
    /// The one shape every API response has, success or failure:
    /// <c>{ code, messageKey, timestamp, data }</c>.
    /// <para>
    /// <see cref="Code"/> repeats the HTTP status, so a client reading only the body
    /// still knows the outcome. <see cref="MessageKey"/> is a translation key, not text:
    /// the client owns the wording and the language. On failure it also identifies the
    /// error, so clients branch on it. <see cref="Data"/> is the payload, null on failure.
    /// </para>
    /// </summary>
    public sealed class ApiResponse
    {
        /// <summary>Message key of every successful response.</summary>
        public const string OkKey = "common.ok";

        public int Code { get; init; }

        public string MessageKey { get; init; } = OkKey;

        public DateTime Timestamp { get; init; }

        /// <summary>
        /// Always written, even when null — the API otherwise omits null properties, and
        /// a missing <c>data</c> would make every client check for two cases.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public object? Data { get; init; }

        public static ApiResponse Success(int status, object? data) => new()
        {
            Code = status,
            MessageKey = OkKey,
            Timestamp = DateTime.UtcNow,
            Data = data
        };

        public static ApiResponse Failure(int status, Error error) => Failure(status, error.MessageKey);

        public static ApiResponse Failure(int status, string messageKey) => new()
        {
            Code = status,
            MessageKey = messageKey,
            Timestamp = DateTime.UtcNow,
            Data = null
        };

        /// <summary>
        /// For failures that did not come from an <see cref="Error"/> — framework
        /// responses such as an unknown route or an unsupported content type.
        /// </summary>
        public static string MessageKeyForStatus(int status) => status switch
        {
            400 => ErrorCodes.ValidationFailed.MessageKey,
            401 => ErrorCodes.Unauthenticated.MessageKey,
            403 => ErrorCodes.Forbidden.MessageKey,
            429 => ErrorCodes.TooManyAttempts.MessageKey,
            404 => "errors.http.notFound",
            405 => "errors.http.methodNotAllowed",
            415 => "errors.http.unsupportedMediaType",
            503 => "errors.http.serviceUnavailable",
            _ => ErrorCodes.Internal.MessageKey
        };
    }
}
