using Microsoft.Extensions.Logging;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services
{
    /// <summary>
    /// Moves a user's trip seats to their new address.
    ///
    /// <para>
    /// Participant rows carry the email alongside the UserId — invitations, the roster
    /// and the per-trip uniqueness rule all read it. Left on the old address, the owner
    /// would see an address the user no longer has, and inviting the new one would
    /// create a second seat for the same person.
    /// </para>
    ///
    /// <para>
    /// Deliberately does not catch: see <see cref="IUserEmailChangedHandler"/>.
    /// </para>
    /// </summary>
    public sealed class SyncParticipantsOnEmailChangedHandler : IUserEmailChangedHandler
    {
        private readonly ITripRepository _trips;
        private readonly ILogger<SyncParticipantsOnEmailChangedHandler> _logger;

        public SyncParticipantsOnEmailChangedHandler(
            ITripRepository trips,
            ILogger<SyncParticipantsOnEmailChangedHandler> logger)
        {
            _trips = trips;
            _logger = logger;
        }

        public async Task HandleAsync(
            Guid userId, string previousEmail, string newEmail, CancellationToken ct = default)
        {
            await _trips.ReassignParticipantEmailAsync(userId, newEmail, ct);

            _logger.LogInformation("Moved trip seats of user {UserId} to their new address.", userId);
        }
    }
}
