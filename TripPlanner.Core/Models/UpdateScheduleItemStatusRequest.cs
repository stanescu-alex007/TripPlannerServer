using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class UpdateScheduleItemStatusRequest
    {
        /// <summary>"Pending" | "InProgress" | "Done" | "Canceled"</summary>
        [Required]
        public string Status { get; set; } = string.Empty;
    }
}
