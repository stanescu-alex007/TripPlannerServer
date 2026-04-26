using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripPlanner.Core.Entities
{
    public class TripParticipant
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        // Nullable — participant may not have a registered account
        public Guid? UserId { get; set; }
        public User? User { get; set; }

        // Always stored directly (no longer derived from User.Email)
        public string Email { get; set; } = string.Empty;

        public string Nickname { get; set; } = string.Empty;
        public string Role { get; set; } = "Participant";

        // Confirmed = user accepted the invite; NotConfirmed = pending
        public string Status { get; set; } = "NotConfirmed";
    }
}
