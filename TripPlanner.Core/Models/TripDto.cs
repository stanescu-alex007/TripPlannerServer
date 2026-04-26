namespace TripPlanner.Core.Models
{
    public class TripDto
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Budget { get; set; }
        public string TransportType { get; set; } = string.Empty;
        public string? Image { get; set; }
        public string? Description { get; set; }

        /// <summary>"upcoming" | "active" | "completed" — computed from dates</summary>
        public string Status { get; set; } = string.Empty;

        public List<TripParticipantDto> Participants { get; set; } = new();
    }
}
