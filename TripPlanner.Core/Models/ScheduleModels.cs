using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class TripScheduleItemDto
    {
        public Guid Id { get; set; }
        public Guid TripId { get; set; }

        /// <summary>Calendar day, serialized as "yyyy-MM-dd" — no time, no timezone.</summary>
        public string Date { get; set; } = string.Empty;

        /// <summary>"HH:mm", or null when the activity has no fixed time.</summary>
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
        public int Order { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateScheduleItemRequest
    {
        /// <summary>
        /// Calendar day as "yyyy-MM-dd". A plain date rather than a DateTime on purpose:
        /// a DateTime would be parsed with an unspecified kind and then shifted by
        /// timezone conversion.
        /// </summary>
        [Required]
        [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Date must be in yyyy-MM-dd format.")]
        public string Date { get; set; } = string.Empty;

        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "StartTime must be in HH:mm format.")]
        public string? StartTime { get; set; }

        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "EndTime must be in HH:mm format.")]
        public string? EndTime { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int Order { get; set; }
    }

    public class UpdateScheduleItemRequest : CreateScheduleItemRequest
    {
    }

    public class UpdateScheduleItemStatusRequest
    {
        /// <summary>"Pending" | "InProgress" | "Done" | "Canceled" (case-insensitive).</summary>
        [Required]
        public string Status { get; set; } = string.Empty;
    }
}
