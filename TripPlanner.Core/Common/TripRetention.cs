namespace TripPlanner.Core.Common;

/// <summary>
/// How long a deleted trip is kept before it is removed from the database for good.
///
/// <para>
/// Deleting a trip takes it away from everyone on it at once, so it is worth being able
/// to undo. After the window the rows are physically deleted — a soft-deleted row that
/// nobody can ever see again is just cost.
/// </para>
/// </summary>
public static class TripRetention
{
    public const int DeletedTripDays = 14;

    public static readonly TimeSpan DeletedTripWindow = TimeSpan.FromDays(DeletedTripDays);
}
