namespace TripPlanner.Core.Common;

/// <summary>
/// Raised when the unique index on <c>Users.Email</c> rejects an insert — i.e. two
/// registrations for the same address raced past the pre-check. The database, not the
/// application, is the authority on uniqueness.
/// </summary>
public sealed class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string email, Exception inner)
        : base($"An account already exists for '{email}'.", inner)
    {
        Email = email;
    }

    public string Email { get; }
}
