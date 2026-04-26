using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class AddParticipantRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Nickname { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Participant";
    }
}
