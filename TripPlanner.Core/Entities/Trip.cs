using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    public class Trip : IAuditable, ISoftDeletable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OwnerId { get; set; }
        public User Owner { get; set; } = null!;

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

        public string TransportType { get; set; } = string.Empty;
        public string? Image { get; set; }
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<TripParticipant> Participants { get; set; } = new List<TripParticipant>();
        public ICollection<TripScheduleItem> ScheduleItems { get; set; } = new List<TripScheduleItem>();
    }
}
