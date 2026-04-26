using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class UpdateTripRequest
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(300)]
        public string Location { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Budget { get; set; }

        [Required]
        [MaxLength(50)]
        public string TransportType { get; set; } = "plane";

        [MaxLength(500)]
        public string? Image { get; set; }

        [MaxLength(2000)]
        public string? Description { get; set; }

        public List<AddParticipantRequest>? Participants { get; set; }
    }
}
