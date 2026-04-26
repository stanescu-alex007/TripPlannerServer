namespace TripPlanner.Core.Entities
{
    public class TripScheduleItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        /// <summary>The date (day) this activity belongs to.</summary>
        public DateTime Date { get; set; }

        /// <summary>Start time in "HH:mm" format, e.g. "09:00". Nullable.</summary>
        public string? StartTime { get; set; }

        /// <summary>End time in "HH:mm" format, e.g. "10:30". Nullable.</summary>
        public string? EndTime { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>"Pending" | "InProgress" | "Done" | "Canceled"</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Sort order within the same day.</summary>
        public int Order { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
