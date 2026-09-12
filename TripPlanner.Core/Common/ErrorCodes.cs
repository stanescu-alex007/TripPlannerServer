namespace TripPlanner.Core.Common;

/// <summary>
/// The full catalogue of errors the API can return.
///
/// Every entry is a stable <c>code</c> string plus the HTTP category it maps to.
/// The frontend should switch on the code and never parse the message.
/// </summary>
public static class ErrorCodes
{
    // ── Authentication ────────────────────────────────────────────────────────

    /// <summary>
    /// Wrong email OR wrong password — deliberately the same code for both, so the
    /// endpoint cannot be used to discover which emails are registered.
    /// </summary>
    public static readonly Error InvalidCredentials = new(
        "AUTH_INVALID_CREDENTIALS",
        "Email or password is incorrect.",
        ErrorType.Unauthenticated);

    /// <summary>Credentials are correct, but the account has not confirmed its email yet.</summary>
    public static readonly Error EmailNotVerified = new(
        "AUTH_EMAIL_NOT_VERIFIED",
        "Please confirm your email address before logging in.",
        ErrorType.Forbidden);

    /// <summary>No access token, or an expired/malformed one.</summary>
    public static readonly Error Unauthenticated = new(
        "AUTH_UNAUTHENTICATED",
        "You are not signed in.",
        ErrorType.Unauthenticated);

    /// <summary>Signed in, but lacking the rights for this action.</summary>
    public static readonly Error Forbidden = new(
        "AUTH_FORBIDDEN",
        "You do not have permission to perform this action.",
        ErrorType.Forbidden);

    public static readonly Error TooManyAttempts = new(
        "AUTH_TOO_MANY_ATTEMPTS",
        "Too many attempts. Please wait a moment and try again.",
        ErrorType.TooManyRequests);

    // ── Registration ──────────────────────────────────────────────────────────

    public static readonly Error EmailAlreadyRegistered = new(
        "AUTH_EMAIL_ALREADY_EXISTS",
        "An account with this email already exists.",
        ErrorType.Conflict);

    public static readonly Error EmailAlreadyVerified = new(
        "AUTH_EMAIL_ALREADY_VERIFIED",
        "This email address has already been confirmed.",
        ErrorType.Conflict);

    // ── Refresh token / session ───────────────────────────────────────────────

    public static readonly Error RefreshTokenMissing = new(
        "AUTH_REFRESH_TOKEN_MISSING",
        "No refresh token was supplied.",
        ErrorType.Unauthenticated);

    public static readonly Error RefreshTokenInvalid = new(
        "AUTH_REFRESH_TOKEN_INVALID",
        "This session is no longer valid. Please sign in again.",
        ErrorType.Unauthenticated);

    public static readonly Error RefreshTokenExpired = new(
        "AUTH_REFRESH_TOKEN_EXPIRED",
        "Your session has expired. Please sign in again.",
        ErrorType.Unauthenticated);

    /// <summary>
    /// An already-rotated refresh token was presented again — the token was either
    /// stolen or replayed. The whole token family has been revoked as a result.
    /// </summary>
    public static readonly Error RefreshTokenReused = new(
        "AUTH_REFRESH_TOKEN_REUSED",
        "This session was ended for security reasons. Please sign in again.",
        ErrorType.Unauthenticated);

    /// <summary>The family hit its absolute lifetime cap; re-authentication is required.</summary>
    public static readonly Error SessionExpired = new(
        "AUTH_SESSION_EXPIRED",
        "Your session has reached its maximum lifetime. Please sign in again.",
        ErrorType.Unauthenticated);

    // ── Passwords ─────────────────────────────────────────────────────────────

    /// <summary>Change-password: the supplied current password is wrong.</summary>
    public static readonly Error CurrentPasswordIncorrect = new(
        "AUTH_CURRENT_PASSWORD_INCORRECT",
        "The current password you entered is incorrect.",
        ErrorType.Validation);

    public static readonly Error PasswordTooWeak = new(
        "AUTH_PASSWORD_TOO_WEAK",
        "Password does not meet the minimum requirements.",
        ErrorType.Validation);

    public static readonly Error PasswordSameAsCurrent = new(
        "AUTH_PASSWORD_SAME_AS_CURRENT",
        "The new password must be different from the current one.",
        ErrorType.Validation);

    // ── One-time tokens (email verification / password reset) ─────────────────

    public static readonly Error TokenInvalid = new(
        "TOKEN_INVALID",
        "This link is not valid.",
        ErrorType.Validation);

    public static readonly Error TokenExpired = new(
        "TOKEN_EXPIRED",
        "This link has expired. Please request a new one.",
        ErrorType.Validation);

    public static readonly Error TokenAlreadyUsed = new(
        "TOKEN_ALREADY_USED",
        "This link has already been used.",
        ErrorType.Validation);

    // ── Generic ───────────────────────────────────────────────────────────────

    public static readonly Error ValidationFailed = new(
        "VALIDATION_FAILED",
        "Some of the submitted data is invalid.",
        ErrorType.Validation);

    public static readonly Error UserNotFound = new(
        "USER_NOT_FOUND",
        "User not found.",
        ErrorType.NotFound);

    public static readonly Error Internal = new(
        "INTERNAL_ERROR",
        "Something went wrong on our side. Please try again.",
        ErrorType.Internal);

    // ── Trips (used from the next phase onward) ───────────────────────────────

    public static readonly Error TripNotFound = new(
        "TRIP_NOT_FOUND",
        "Trip not found.",
        ErrorType.NotFound);

    public static readonly Error TripForbidden = new(
        "TRIP_FORBIDDEN",
        "You do not have access to this trip.",
        ErrorType.Forbidden);

    public static readonly Error TripDateRangeInvalid = new(
        "TRIP_DATE_RANGE_INVALID",
        "The end date must be on or after the start date.",
        ErrorType.Validation);

    public static readonly Error ParticipantAlreadyExists = new(
        "PARTICIPANT_ALREADY_EXISTS",
        "This person is already a participant on the trip.",
        ErrorType.Conflict);

    public static readonly Error ParticipantNotFound = new(
        "PARTICIPANT_NOT_FOUND",
        "Participant not found.",
        ErrorType.NotFound);

    public static readonly Error OwnerCannotBeRemoved = new(
        "PARTICIPANT_OWNER_IMMUTABLE",
        "The trip owner cannot be removed.",
        ErrorType.Validation);

    public static readonly Error ScheduleItemNotFound = new(
        "SCHEDULE_ITEM_NOT_FOUND",
        "Schedule item not found.",
        ErrorType.NotFound);

    public static readonly Error ScheduleDateOutOfRange = new(
        "SCHEDULE_DATE_OUT_OF_RANGE",
        "The activity date must fall within the trip dates.",
        ErrorType.Validation);

    public static readonly Error ScheduleStatusInvalid = new(
        "SCHEDULE_STATUS_INVALID",
        "Unknown schedule status.",
        ErrorType.Validation);
}
