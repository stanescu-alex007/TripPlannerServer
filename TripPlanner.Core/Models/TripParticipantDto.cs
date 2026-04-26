namespace TripPlanner.Core.Models
{
    public class TripParticipantDto
    {
        public Guid Id { get; set; }
        public Guid? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
