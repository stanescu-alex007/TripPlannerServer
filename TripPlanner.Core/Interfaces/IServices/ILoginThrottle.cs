using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Per-account progressive delay after failed sign-in attempts. Per account rather
    /// than per IP: IP limits do nothing against an attacker rotating addresses.
    /// </summary>
    public interface ILoginThrottle
    {
        /// <summary>True while the delay owed by recent failures has not yet elapsed.</summary>
        bool IsThrottled(User user);

        Task RecordFailureAsync(User user, CancellationToken ct = default);

        Task ResetAsync(User user, CancellationToken ct = default);
    }
}
