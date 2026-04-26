namespace TripPlanner.Core.Models
{
    public class TripScheduleItemDto
    {
        public Guid Id { get; set; }
        public Guid TripId { get; set; }
        public DateTime Date { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Status { get; set; } = "Pending";
        public int Order { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
