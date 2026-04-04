using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TripPlanner.Core.Entities
{
    public class TripParticipant
    {
        public Guid TripId { get; set; }
        public Trip Trip { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }
    }
}
