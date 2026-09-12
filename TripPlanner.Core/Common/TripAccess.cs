using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Common;

/// <summary>
/// The one place that decides who may do what on a trip.
///
/// <para>
/// Until now every check was <c>trip.OwnerId != requesterId</c>, so the Role column was
/// decorative: an "Admin" could do nothing an outsider could not, and a participant could
/// not even read their own trip. These rules give the roles meaning.
/// </para>
/// </summary>
public static class TripAccess
{
    /// <summary>Owner, or any participant who has not declined.</summary>
    public static bool CanView(Trip trip, Guid userId) =>
        trip.OwnerId == userId || FindParticipant(trip, userId) is { } p
            && !string.Equals(p.Status, TripParticipantStatus.Declined, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Edit trip details and manage the schedule: owner or a participant with the Admin
    /// role, and only once they have accepted the invitation.
    /// </summary>
    public static bool CanManage(Trip trip, Guid userId)
    {
        if (trip.OwnerId == userId) return true;

        var participant = FindParticipant(trip, userId);

        return participant is not null
               && string.Equals(participant.Role, TripParticipantRole.Admin, StringComparison.OrdinalIgnoreCase)
               && string.Equals(participant.Status, TripParticipantStatus.Confirmed, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deleting a trip, transferring it, or changing who is on it stays with the owner —
    /// an Admin must not be able to remove the owner or hand the trip away.
    /// </summary>
    public static bool IsOwner(Trip trip, Guid userId) => trip.OwnerId == userId;

    private static TripParticipant? FindParticipant(Trip trip, Guid userId) =>
        trip.Participants?.FirstOrDefault(p => p.UserId == userId);
}
