namespace TripPlanner.Core.Models
{
    /// <summary>
    /// What an invitation link shows before the recipient commits to anything.
    /// Served anonymously, so it carries only what is already in the email —
    /// nothing about the trip's participants, budget or schedule.
    /// </summary>
    public sealed class InvitationPreviewDto
    {
        public Guid TripId { get; set; }
        public string TripName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;

        /// <summary>"yyyy-MM-dd".</summary>
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;

        /// <summary>Who is inviting — a first name, not the owner's full record.</summary>
        public string InvitedBy { get; set; } = string.Empty;

        /// <summary>The address the invitation was sent to; accepting must match it.</summary>
        public string InvitedEmail { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        /// <summary>False once the invitation is expired, withdrawn or already answered.</summary>
        public bool IsOpen { get; set; }

        /// <summary>Set when already answered, so the page can say which way.</summary>
        public string? Status { get; set; }

        /// <summary>
        /// True when no account exists for the invited address, so the page can offer
        /// registration rather than a sign-in form the recipient cannot use.
        /// </summary>
        public bool RequiresRegistration { get; set; }
    }

    /// <summary>An invitation waiting for the signed-in user, listed inside the app.</summary>
    public sealed class PendingInvitationDto
    {
        public Guid TripId { get; set; }
        public string TripName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string InvitedBy { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime InvitedAt { get; set; }

        // No token here: only its hash is stored, so there is nothing to hand back.
        // A signed-in recipient does not need one anyway — the invitation is found by
        // their own email, and the trip id is enough to answer it.
    }
}
