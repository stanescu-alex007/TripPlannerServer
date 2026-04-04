namespace TripPlanner.Core.Entities
{
    public class Trip
    {
        public Guid Id { get; set; }

        public Guid OwnerId { get; set; }
        public User Owner { get; set; }

        public string Name { get; set; }
        public string Location { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal Budget { get; set; }

        public string TransportType { get; set; }
        public string Image { get; set; }
        public string Description { get; set; }

        public ICollection<TripParticipant> Participants { get; set; }
    }
}
