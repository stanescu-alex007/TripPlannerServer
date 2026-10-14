using TripPlanner.Core.Common;

namespace TripPlanner.Core.Entities
{
    /// <summary>
    /// The outstanding invitation for one participant.
    ///
    /// <para>
    /// Separate from <see cref="UserToken"/> because an invitee often has no account
    /// yet — the whole point is to reach someone who is not a user. The token is
    /// therefore tied to the participant row, not to a user.
    /// </para>
    ///
    /// <para>
    /// One live invitation per participant: re-inviting replaces the previous token
    /// rather than leaving several valid links in different inboxes.
    /// </para>
    /// </summary>
    public class TripInvitation : IAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid TripParticipantId { get; set; }
        public TripParticipant Participant { get; set; } = null!;

        /// <summary>SHA-256 of the raw token; only the email carries the raw value.</summary>
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        /// <summary>Last time the invitation email actually went out.</summary>
        public DateTime SentAt { get; set; }

        /// <summary>How many times it has been sent, so pestering can be rate limited.</summary>
        public int SendCount { get; set; } = 1;

        /// <summary>Set once accepted or declined; the participant's status holds which.</summary>
        public DateTime? RespondedAt { get; set; }

        /// <summary>Set when superseded by a newer invitation, or the invite was withdrawn.</summary>
        public DateTime? InvalidatedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public bool IsOpen => RespondedAt is null
                              && InvalidatedAt is null
                              && ExpiresAt > DateTime.UtcNow;
    }
}
