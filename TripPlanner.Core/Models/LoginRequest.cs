using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class LoginRequest
    {
        [Required, EmailAddress, MaxLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Password { get; set; } = string.Empty;
    }
}
