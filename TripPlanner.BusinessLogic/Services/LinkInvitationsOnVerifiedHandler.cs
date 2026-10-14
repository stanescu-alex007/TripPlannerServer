using Microsoft.Extensions.Logging;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services
{
    /// <summary>
    /// When an account finishes verification, attaches any trip invitations that were
    /// addressed to that email before the account existed.
    ///
    /// <para>
    /// This lives on the trips side of the fence and subscribes to the event, so the
    /// dependency runs trips → auth rather than the other way round.
    /// </para>
    /// </summary>
    public sealed class LinkInvitationsOnVerifiedHandler : IUserVerifiedHandler
    {
        private readonly ITripInvitationService _invitations;
        private readonly ILogger<LinkInvitationsOnVerifiedHandler> _logger;

        public LinkInvitationsOnVerifiedHandler(
            ITripInvitationService invitations,
            ILogger<LinkInvitationsOnVerifiedHandler> logger)
        {
            _invitations = invitations;
            _logger = logger;
        }

        public async Task HandleAsync(Guid userId, string email, CancellationToken ct = default)
        {
            try
            {
                await _invitations.LinkInvitationsToNewUserAsync(userId, email, ct);
            }
            catch (Exception ex)
            {
                // The verification itself already succeeded and must stand. A missed
                // link only means the invitation is found through its email link
                // instead of the in-app list.
                _logger.LogError(ex,
                    "Could not link pending invitations for user {UserId}.", userId);
            }
        }
    }
}
