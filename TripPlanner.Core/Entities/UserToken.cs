using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    /// <summary>
    /// A single-use, short-lived token sent to the user by email. One table serves both
    /// email verification and password reset — same mechanics, different
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

        public DateTime ExpiresAt { get; set; }

        /// <summary>Stamped the first time the token is redeemed; enforces single use.</summary>
        public DateTime? UsedAt { get; set; }

        /// <summary>
        /// Stamped when the token is retired without being redeemed — for example when a
        /// newer token for the same purpose supersedes it.
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
    }
}
