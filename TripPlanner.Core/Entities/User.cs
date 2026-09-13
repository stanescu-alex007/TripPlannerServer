using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    public class User : IAuditable, ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        /// <summary>Always stored lower-cased and trimmed; unique across the table.</summary>
        public string Email { get; set; } = string.Empty;

        public DateTime? BirthDate { get; set; }

        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Null for accounts that only sign in through an external provider. Such a
        /// user has no password to verify, so every password path must handle this.
        /// </summary>
        public string? PasswordHash { get; set; }

        /// <summary>
        /// Set once the user follows the verification link. Null means the account
        /// exists but cannot sign in yet.
        /// </summary>
        public DateTime? EmailVerifiedAt { get; set; }

        public bool IsEmailVerified => EmailVerifiedAt.HasValue;

        /// <summary>Stamped on every password change or reset.</summary>
        public DateTime? PasswordChangedAt { get; set; }

        // ── Failed sign-in throttling ─────────────────────────────────────────
        // Per account, not per IP: IP rate limiting alone does nothing against an
        // attacker who rotates addresses while hammering one account.

        public int FailedLoginAttempts { get; set; }

        /// <summary>
        /// When the most recent failure happened. Combined with the attempt count it
        /// yields a delay that grows with each failure — no hard lockout, so nobody
        /// can deliberately lock a user out of their own account.
        /// </summary>
        public DateTime? LastFailedLoginAt { get; set; }

        // ── Two-factor ────────────────────────────────────────────────────────

        /// <summary>
        /// Base32 TOTP shared secret. Only set while 2FA is enabled, and cleared the
        /// moment it is switched off.
        /// </summary>
        public string? TwoFactorSecret { get; set; }

        /// <summary>
        /// Non-null only after the user proves they can generate a valid code, so a
        /// half-finished setup never locks anyone out.
        /// </summary>
        public DateTime? TwoFactorEnabledAt { get; set; }

        public bool IsTwoFactorEnabled => TwoFactorEnabledAt.HasValue;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<Trip> OwnedTrips { get; set; } = new List<Trip>();
        public ICollection<TripParticipant> TripParticipants { get; set; } = new List<TripParticipant>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<UserToken> UserTokens { get; set; } = new List<UserToken>();
        public ICollection<UserRecoveryCode> RecoveryCodes { get; set; } = new List<UserRecoveryCode>();
        public ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();
    }
}
