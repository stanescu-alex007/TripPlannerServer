using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    public class TripScheduleItem : IAuditable, ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        /// <summary>The day this activity belongs to, stored as UTC midnight.</summary>
        public DateTime Date { get; set; }

        /// <summary>Start time of day, e.g. 09:00. Null when the activity has no fixed time.</summary>
        public TimeOnly? StartTime { get; set; }

        /// <summary>End time of day, e.g. 10:30.</summary>
        public TimeOnly? EndTime { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>See <see cref="TripScheduleItemStatus"/>.</summary>
        public string Status { get; set; } = TripScheduleItemStatus.Pending;

        /// <summary>Sort order within the same day.</summary>
        public int Order { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public static class TripScheduleItemStatus
    {
        public const string Pending = "Pending";
        public const string InProgress = "InProgress";
        public const string Done = "Done";
        public const string Canceled = "Canceled";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Pending, InProgress, Done, Canceled };

        /// <summary>
        /// Maps any casing the client sends onto the canonical value.
        /// Returns null when the input is not a known status.
        /// </summary>
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            return All.FirstOrDefault(s => string.Equals(s, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
