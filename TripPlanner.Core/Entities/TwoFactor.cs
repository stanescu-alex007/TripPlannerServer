using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    /// <summary>
    /// A single-use code that gets the user back in when their authenticator app is
    /// gone. Stored hashed, exactly like a password — anyone reading the table must
    /// not be able to bypass 2FA with it.
    /// </summary>
    public class UserRecoveryCode : IAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public string CodeHash { get; set; } = string.Empty;

        public DateTime? UsedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsUsed => UsedAt.HasValue;
    }

    /// <summary>
    /// Links an account to an identity from an external provider.
    ///
    /// <para>
    /// Nothing writes to this yet — the table exists so that adding Google, GitHub or
    /// any other provider later is a matter of implementing the handshake, not a
    /// migration that has to touch live accounts.
    /// </para>
    /// </summary>
    public class ExternalLogin : IAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>"google", "github", … — lower-case, stable.</summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>The provider's own immutable id for the user (never the email).</summary>
        public string ProviderKey { get; set; } = string.Empty;

        /// <summary>Email as the provider reported it, for display and matching.</summary>
        public string? ProviderEmail { get; set; }

        public string? DisplayName { get; set; }
        public string? PictureUrl { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public static class ExternalLoginProvider
    {
        public const string Google = "google";
        public const string GitHub = "github";
        public const string Facebook = "facebook";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Google, GitHub, Facebook };
    }
}
