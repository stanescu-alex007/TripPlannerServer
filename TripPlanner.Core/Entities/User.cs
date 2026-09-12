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

        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Set once the user follows the verification link. Null means the account
        /// exists but cannot sign in yet.
        /// </summary>
        public DateTime? EmailVerifiedAt { get; set; }

        public bool IsEmailVerified => EmailVerifiedAt.HasValue;

        /// <summary>Stamped on every password change or reset.</summary>
        public DateTime? PasswordChangedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<Trip> OwnedTrips { get; set; } = new List<Trip>();
        public ICollection<TripParticipant> TripParticipants { get; set; } = new List<TripParticipant>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<UserToken> UserTokens { get; set; } = new List<UserToken>();
    }
}
