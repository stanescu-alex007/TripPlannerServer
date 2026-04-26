using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class RegisterRequest
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public DateTime BirthDate { get; set; }

        [Required, MinLength(8), MaxLength(200)]
        public string Password { get; set; } = string.Empty;
    }
}
