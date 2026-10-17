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

        /// <summary>Lets the UI show the right state without a second call.</summary>
        public bool IsTwoFactorEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Email is intentionally absent: changing it has to re-verify the new address, so it
    /// needs its own flow rather than riding along with the profile. The frontend used to
    /// send it here and silently have it dropped.
    /// <para>
    /// The photo is absent too: it is uploaded through <c>POST /api/user/me/photo</c>.
    /// Accepting any URL here let someone point their avatar at a server they control and
    /// learn the IP address of everyone who looked at it.
    /// </para>
    /// </summary>
    public class UpdateUserRequest
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Phone, MaxLength(30)]
        public string? Phone { get; set; }

        public DateTime? BirthDate { get; set; }
    }

    // ── Personal data export ──────────────────────────────────────────────────

    /// <summary>
    /// What the user gets from "Download my data". Secrets are left out on purpose —
    /// the password hash, the 2FA secret and token hashes are useless to the person and
    /// dangerous in a file that may be stored carelessly. Other people's email addresses
    /// are left out too: they are those people's data, not the user's.
    /// </summary>
    public sealed class UserDataExport
    {
        public DateTime ExportedAt { get; set; }
        public ExportedProfile Profile { get; set; } = new();
        public List<ExportedTrip> Trips { get; set; } = new();
        public List<ExportedSession> ActiveSessions { get; set; } = new();
    }

    public sealed class ExportedProfile
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime? BirthDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }
        public DateTime? PasswordChangedAt { get; set; }
        public DateTime? TwoFactorEnabledAt { get; set; }
    }

    public sealed class ExportedTrip
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Budget { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string TransportType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Image { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool IsOwner { get; set; }

        /// <summary>The user's own seat; null on a trip they own but hold no seat on.</summary>
        public string? MyNickname { get; set; }
        public string? MyRole { get; set; }
        public string? MyStatus { get; set; }

        /// <summary>Nicknames only — see <see cref="UserDataExport"/>.</summary>
        public List<string> OtherParticipants { get; set; } = new();

        public List<ExportedScheduleItem> Schedule { get; set; } = new();
    }

    public sealed class ExportedScheduleItem
    {
        public DateTime Date { get; set; }
        public TimeOnly? StartTime { get; set; }
        public TimeOnly? EndTime { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public sealed class ExportedSession
    {
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
