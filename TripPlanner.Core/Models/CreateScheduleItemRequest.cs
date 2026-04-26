using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class CreateScheduleItemRequest
    {
        [Required]
        public DateTime Date { get; set; }

        /// <summary>"HH:mm" — e.g. "09:00"</summary>
        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "StartTime must be in HH:mm format.")]
        public string? StartTime { get; set; }

        /// <summary>"HH:mm" — e.g. "10:30"</summary>
        [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "EndTime must be in HH:mm format.")]
        public string? EndTime { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int Order { get; set; } = 0;
    }
}
