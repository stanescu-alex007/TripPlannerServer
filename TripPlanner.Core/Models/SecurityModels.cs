using System.ComponentModel.DataAnnotations;

namespace TripPlanner.Core.Models
{
    // ── Responses ─────────────────────────────────────────────────────────────

    public sealed record TwoFactorSetupResponse(
        /// <summary>Base32 secret, so it can be typed in when a camera is unavailable.</summary>
        string Secret,

        /// <summary>otpauth:// URI for the client to render as a QR code.</summary>
        string OtpAuthUri);

    /// <summary>
    /// Recovery codes in plain text. This is the only time they exist outside a hash —
    /// the server cannot show them again.
    /// </summary>
    public sealed record RecoveryCodesResponse(IReadOnlyList<string> Codes);

    public sealed class SessionDto
    {
        /// <summary>The family id — what "a device I am signed in on" maps to.</summary>
        public Guid Id { get; set; }

        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }

        /// <summary>Coarse label derived from the user agent, e.g. "Chrome on Windows".</summary>
        public string DeviceLabel { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        /// <summary>True for the session making the request — never offer to end it by mistake.</summary>
        public bool IsCurrent { get; set; }
    }

    // ── Requests ──────────────────────────────────────────────────────────────

    public class TwoFactorLoginRequest
    {
        [Required]
        public string ChallengeToken { get; set; } = string.Empty;

        /// <summary>A six-digit authenticator code, or one recovery code.</summary>
        [Required, MaxLength(50)]
        public string Code { get; set; } = string.Empty;
    }

    public class EnableTwoFactorRequest
    {
        /// <summary>The secret handed out by the setup call, echoed back on confirm.</summary>
        [Required, MaxLength(128)]
        public string Secret { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>
    /// Used for anything that weakens the account. Re-asking for the password means a
    /// hijacked session alone is not enough to strip the protections off.
    /// </summary>
    public class PasswordConfirmationRequest
    {
        [Required, MaxLength(200)]
        public string Password { get; set; } = string.Empty;
    }

    public class RequestEmailChangeRequest
    {
        [Required, EmailAddress, MaxLength(256)]
        public string NewEmail { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Password { get; set; } = string.Empty;
    }

    public class ConfirmEmailChangeRequest
    {
        [Required]
        public string Token { get; set; } = string.Empty;
    }
}
