namespace TripPlanner.Core.Common;

/// <summary>
/// Raised when a save finds the row already changed by someone else since it was read —
/// for example an ownership offer answered at the same moment it lapsed. The caller
/// reports the state that won instead of overwriting it.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Exception inner)
        : base("The record was changed by another operation.", inner)
    {
    }
}
