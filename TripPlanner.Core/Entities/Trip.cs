using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    public class Trip : IAuditable, ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Null once the owner has deleted their account, while the trip is offered to
        /// whoever is next in line. If nobody takes it, the trip is deleted.
        /// </summary>
        public Guid? OwnerId { get; set; }
        public User? Owner { get; set; }

        /// <summary>
        /// Who moved the trip to their deleted list, so it can be offered back to them —
        /// and only them — until it is purged. Null when nobody deleted it deliberately:
        /// its owner left and no one took it over, so it is gone for good.
        /// </summary>
        public Guid? DeletedByUserId { get; set; }

        /// <summary>The owner left and an ownership offer is still in progress.</summary>
        public bool IsAwaitingOwner => OwnerId is null && DeletedAt is null;

        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// Calendar day the trip starts, stored as UTC midnight. Always build it with
        /// <c>DateTime.SpecifyKind(value.Date, DateTimeKind.Utc)</c> — never
        /// <c>ToUniversalTime()</c>, which reinterprets an unspecified date as local time
        /// and shifts the day.
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>Calendar day the trip ends, inclusive. Stored as UTC midnight.</summary>
        public DateTime EndDate { get; set; }

        public decimal Budget { get; set; }

        /// <summary>
        /// ISO 4217 code. Without it the amount silently assumed euros, which for a
        /// travel app is exactly the kind of guess that goes wrong quietly.
        /// </summary>
        public string Currency { get; set; } = "EUR";

        public string TransportType { get; set; } = string.Empty;
        public string? Image { get; set; }
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<TripParticipant> Participants { get; set; } = new List<TripParticipant>();
        public ICollection<TripScheduleItem> ScheduleItems { get; set; } = new List<TripScheduleItem>();
        public ICollection<TripOwnershipTransfer> OwnershipTransfers { get; set; } = new List<TripOwnershipTransfer>();
    }
}
