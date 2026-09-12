using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    /// <summary>
    /// One link in a rotating refresh-token chain.
    ///
    /// <para>
    /// Every sign-in starts a new <see cref="FamilyId"/>. Each refresh revokes the
    /// current link and issues the next one inside the same family. Presenting a link
    /// that is already revoked means the token leaked or was replayed, so the entire
    /// family is detonated — see <c>RefreshTokenRevokeReason.ReuseDetected</c>.
    /// </para>
    ///
    /// <para>
    /// Never soft-deleted: expired rows are pruned for real, and a revoked row must
    /// stay readable so reuse can still be detected against it.
    /// </para>
    /// </summary>
    public class RefreshToken : IAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>Shared by every token descended from a single sign-in.</summary>
        public Guid FamilyId { get; set; }

        /// <summary>SHA-256 of the raw token. The raw value never touches the database.</summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>When this individual link stops being usable.</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Hard ceiling for the whole family, copied to every link. Rotation cannot push
        /// it out, so a session can never live forever.
        /// </summary>
        public DateTime FamilyExpiresAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        /// <summary>See <see cref="RefreshTokenRevokeReason"/>.</summary>
        public string? RevokedReason { get; set; }

        /// <summary>The link issued in place of this one. A real FK, not a hash.</summary>
        public Guid? ReplacedByTokenId { get; set; }
        public RefreshToken? ReplacedByToken { get; set; }

        public string? CreatedByIp { get; set; }
        public string? UserAgent { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
    }

    public static class RefreshTokenRevokeReason
    {
        /// <summary>Normal rotation — replaced by the next link in the family.</summary>
        public const string Rotated = "rotated";

        /// <summary>The user signed out.</summary>
        public const string Logout = "logout";

        /// <summary>An already-rotated link was presented again; the family was detonated.</summary>
        public const string ReuseDetected = "reuse-detected";

        /// <summary>Password was changed or reset, invalidating every session.</summary>
        public const string PasswordChanged = "password-changed";
    }
}
