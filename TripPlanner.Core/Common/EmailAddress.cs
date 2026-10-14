namespace TripPlanner.Core.Common;

/// <summary>
/// The single definition of an address's stored form.
///
/// <para>
/// The unique indexes on <c>Users.Email</c> and <c>TripParticipants(TripId, Email)</c>
/// compare with plain equality, so every write and every lookup has to agree on exactly
/// this form. Hand-written copies of the rule in each service could drift apart silently.
/// </para>
/// </summary>
public static class EmailAddress
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
