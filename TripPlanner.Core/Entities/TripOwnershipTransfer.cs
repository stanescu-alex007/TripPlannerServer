using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    /// <summary>
    /// An offer to take over a trip whose owner deleted their account.
    ///
    /// <para>
    /// Offers go out one at a time, in order of standing on the trip. Declining, or
    /// letting the offer lapse, passes it to the next person in line; when nobody is
    /// left the trip is deleted. Every past offer is kept, which is also how the next
    /// candidate is chosen: anyone already passed over is not asked again.
    /// </para>
    /// </summary>
    public class TripOwnershipTransfer : IAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TripId { get; set; }
        public Trip Trip { get; set; } = null!;

        /// <summary>The seat the offer is addressed to — always a registered, confirmed participant.</summary>
        public Guid CandidateParticipantId { get; set; }
        public TripParticipant Candidate { get; set; } = null!;

        /// <summary>See <see cref="TripOwnershipTransferStatus"/>.</summary>
        public string Status { get; set; } = TripOwnershipTransferStatus.Pending;

        public DateTime ExpiresAt { get; set; }

        /// <summary>When the candidate accepted or declined. Null for offers that lapsed or were withdrawn.</summary>
        public DateTime? RespondedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public static class TripOwnershipTransferStatus
    {
        public const string Pending = "Pending";
        public const string Accepted = "Accepted";
        public const string Declined = "Declined";

        /// <summary>Nobody answered in time. Treated exactly like a decline.</summary>
        public const string Expired = "Expired";

        /// <summary>The candidate deleted their own account before answering.</summary>
        public const string Withdrawn = "Withdrawn";
    }
}
