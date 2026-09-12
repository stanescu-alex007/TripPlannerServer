using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    public class TripParticipant : IAuditable, ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        /// <summary>Null while the invitee has no registered account.</summary>
        public Guid? UserId { get; set; }
        public User? User { get; set; }

        /// <summary>Stored lower-cased; unique per trip.</summary>
        public string Email { get; set; } = string.Empty;

        public string Nickname { get; set; } = string.Empty;

        /// <summary>See <see cref="TripParticipantRole"/>.</summary>
        public string Role { get; set; } = TripParticipantRole.Participant;

        /// <summary>See <see cref="TripParticipantStatus"/>.</summary>
        public string Status { get; set; } = TripParticipantStatus.NotConfirmed;

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public static class TripParticipantRole
    {
        public const string Owner = "Owner";
        public const string Admin = "Admin";
        public const string Vip = "VIP";
        public const string Participant = "Participant";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Owner, Admin, Vip, Participant };
    }

    public static class TripParticipantStatus
    {
        public const string Confirmed = "Confirmed";
        public const string NotConfirmed = "NotConfirmed";
        public const string Declined = "Declined";

        public static readonly IReadOnlySet<string> All =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Confirmed, NotConfirmed, Declined };
    }
}
