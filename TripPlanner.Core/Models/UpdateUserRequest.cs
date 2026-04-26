using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class UpdateUserRequest
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Phone, MaxLength(30)]
        public string? Phone { get; set; }

        [Url, MaxLength(500)]
        public string? ImageUrl { get; set; }
    }
}
