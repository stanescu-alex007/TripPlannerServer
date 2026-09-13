using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    /// <summary>
    /// A single-use, short-lived token sent to the user by email. One table serves
    /// verification, password reset and email change — same mechanics, different
    /// <see cref="Purpose"/>.
    /// </summary>
    public class UserToken : IAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>SHA-256 of the raw token. Only the email ever carries the raw value.</summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>See <see cref="UserTokenPurpose"/>.</summary>
        public string Purpose { get; set; } = string.Empty;

        /// <summary>
        /// For <see cref="UserTokenPurpose.EmailChange"/>: the address being moved to.
        /// Held here rather than on the user so an unconfirmed change never touches the
        /// account, and the link itself carries which address it is for.
        /// </summary>
        public string? NewEmail { get; set; }

        public DateTime ExpiresAt { get; set; }

        /// <summary>Stamped the first time the token is redeemed; enforces single use.</summary>
        public DateTime? UsedAt { get; set; }

        /// <summary>
        /// Stamped when the token is retired without being redeemed — for example when
        /// a newer token for the same purpose supersedes it.
        /// </summary>
        public DateTime? InvalidatedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsRedeemable => UsedAt is null
                                    && InvalidatedAt is null
                                    && ExpiresAt > DateTime.UtcNow;
    }

    public static class UserTokenPurpose
    {
        public const string EmailVerification = "email-verification";
        public const string PasswordReset = "password-reset";
        public const string EmailChange = "email-change";

        /// <summary>
        /// Issued when a password check passes but 2FA is still outstanding. Short
        /// lived and single use, so an intercepted challenge is worth almost nothing
        /// without the code that completes it.
        /// </summary>
        public const string TwoFactorChallenge = "two-factor-challenge";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.Ordinal)
            {
                EmailVerification, PasswordReset, EmailChange, TwoFactorChallenge
            };
    }
}
