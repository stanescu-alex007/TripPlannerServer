namespace TripPlanner.Core.Models
{
    /// <summary>The ownership offer in progress on a trip, as everyone on it sees it.</summary>
    public class OwnershipTransferDto
    {
        public Guid Id { get; set; }

        /// <summary>Who the trip is currently offered to.</summary>
        public string CandidateNickname { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        /// <summary>The calling user is the one being asked, so the UI can offer the buttons.</summary>
        public bool IsForYou { get; set; }
    }

    /// <summary>An offer waiting on the signed-in user, for the dashboard.</summary>
    public class OwnershipOfferDto
    {
        public Guid Id { get; set; }
        public Guid TripId { get; set; }
        public string TripName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        /// <summary>"yyyy-MM-dd"</summary>
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// An offer created inside a transaction, carrying what its email needs. Returned
    /// rather than sent on the spot, so no mail goes out for an offer that is then
    /// rolled back.
    /// </summary>
    public sealed record OwnershipOfferNotice(
        Guid TransferId,
        Guid TripId,
        string TripName,
        string CandidateEmail,
        string CandidateNickname,
        DateTime ExpiresAt);
}
