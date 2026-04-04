namespace TripPlanner.Core.Entities
{
    public class User
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }

        public string Email { get; set; }

        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }

        public string PasswordHash { get; set; }

        public ICollection<Trip> OwnedTrips { get; set; }
        public ICollection<TripParticipant> TripParticipants { get; set; }
        public ICollection<RefreshToken> RefreshTokens { get; set; }

    }
}
