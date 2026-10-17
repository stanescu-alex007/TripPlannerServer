using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    /// <summary>
    /// Length limits here are a first line of defence only. The real password policy
    /// lives in <see cref="Common.AuthSettings"/> and is enforced in AuthService, so it
    /// stays in one place and can report a specific error code.
    /// </summary>
    public class RegisterRequest
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        /// <summary>Optional — collected for the profile, not used for authentication.</summary>
        public DateTime? BirthDate { get; set; }

        [Required, MaxLength(200)]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// The Terms and Privacy Policy checkbox. Checked on the server too: the
        /// acceptance is stored as a fact, so it must not rest on the client alone.
        /// </summary>
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Terms and the Privacy Policy.")]
        public bool AcceptTerms { get; set; }
    }

    public class LoginRequest
    {
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Password { get; set; } = string.Empty;
    }

    public class VerifyEmailRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }

    public class ResendVerificationRequest
    {
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        [Required, EmailAddress, MaxLength(256)]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        [Required, MaxLength(200)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
