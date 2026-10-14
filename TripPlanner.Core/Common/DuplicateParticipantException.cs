namespace TripPlanner.Core.Common;

/// <summary>
/// Raised when the unique index on <c>TripParticipants (TripId, Email)</c> rejects an
/// insert — two requests adding the same address to the same trip raced past the
/// pre-check. As with <see cref="DuplicateEmailException"/>, the database is the
/// authority on uniqueness; this lets the business layer answer "already on the trip"
/// instead of surfacing a 500.
/// </summary>
public sealed class DuplicateParticipantException : Exception
{
    public DuplicateParticipantException(Guid tripId, string email, Exception inner)
        : base($"'{email}' is already a participant on trip {tripId}.", inner)
    {
        TripId = tripId;
        Email = email;
    }

    public Guid TripId { get; }

    public string Email { get; }
}
