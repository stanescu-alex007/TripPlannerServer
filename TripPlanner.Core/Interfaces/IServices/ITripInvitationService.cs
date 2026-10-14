using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface ITripInvitationService
    {
        /// <summary>
        /// Issues an invitation for a participant and emails it. Replaces any
        /// outstanding one, so only the newest link ever works.
        /// </summary>
        Task<Result> SendAsync(Guid participantId, CancellationToken ct = default);

        /// <summary>
        /// Sends invitations to everyone on a trip who has not answered yet. Used
        /// right after a trip is created or its roster edited.
        /// </summary>
        Task<int> SendPendingForTripAsync(Guid tripId, CancellationToken ct = default);

        /// <summary>Organiser asks for the link to go out again.</summary>
        Task<Result> ResendAsync(Guid tripId, Guid participantId, Guid requesterId, CancellationToken ct = default);

        /// <summary>
        /// What the link shows before the recipient acts. Anonymous, so it exposes
        /// only what the email already contained.
        /// </summary>
        Task<Result<InvitationPreviewDto>> PreviewAsync(string rawToken, CancellationToken ct = default);

        /// <summary>
        /// Accepts from the email link. Requires a signed-in account whose address
        /// matches the invitation, otherwise the wrong account joins the trip.
        /// </summary>
        Task<Result> AcceptByTokenAsync(string rawToken, Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Declines from the email link. Deliberately open to anyone holding the
        /// token: refusing should not require creating an account first.
        /// </summary>
        Task<Result> DeclineByTokenAsync(string rawToken, CancellationToken ct = default);

        /// <summary>Answers from inside the app, where the invitation is found by the user's email.</summary>
        Task<Result> RespondInAppAsync(Guid tripId, Guid userId, bool accept, CancellationToken ct = default);

        /// <summary>Open invitations addressed to this user, for the in-app list.</summary>
        Task<List<PendingInvitationDto>> GetPendingForUserAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Attaches invitations addressed to an email to a newly created account, so a
        /// user who registers because of an invitation finds it waiting.
        /// </summary>
        Task<int> LinkInvitationsToNewUserAsync(Guid userId, string email, CancellationToken ct = default);
    }
}
