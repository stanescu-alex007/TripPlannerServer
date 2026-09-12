using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    public class UserDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime? BirthDate { get; set; }
        public bool IsEmailVerified { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Email is intentionally absent: changing it has to re-verify the new address, so it
    /// needs its own flow rather than riding along with the profile. The frontend used to
    /// send it here and silently have it dropped.
    /// </summary>
    public class UpdateUserRequest
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Phone, MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(500)]
        [RegularExpression(@"^https?://.+", ErrorMessage = "ImageUrl must be an http(s) URL.")]
        public string? ImageUrl { get; set; }

        public DateTime? BirthDate { get; set; }
    }
}
