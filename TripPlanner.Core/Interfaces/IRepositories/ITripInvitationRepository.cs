using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface ITripInvitationRepository
    {
        /// <summary>
        /// Loads an invitation with its participant and trip, including rows that are
        /// expired or already answered — the caller needs to tell those cases apart
        /// rather than just seeing "not found".
        /// </summary>
        Task<TripInvitation?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

        /// <summary>The live invitation for a seat, if there is one.</summary>
        Task<TripInvitation?> GetOpenForParticipantAsync(Guid participantId, CancellationToken ct = default);

        Task AddAsync(TripInvitation invitation, CancellationToken ct = default);

        Task UpdateAsync(TripInvitation invitation, CancellationToken ct = default);

        /// <summary>Retires outstanding invitations for a seat before a new one is issued.</summary>
        Task<int> InvalidateForParticipantAsync(Guid participantId, CancellationToken ct = default);

        /// <summary>Retires every outstanding invitation on a trip, before it is deleted.</summary>
        Task<int> InvalidateOpenForTripAsync(Guid tripId, CancellationToken ct = default);

        /// <summary>
        /// Open invitations addressed to an email, so someone who lost the message can
        /// still find them in the app.
        /// </summary>
        Task<List<TripInvitation>> GetOpenForEmailAsync(string email, CancellationToken ct = default);

        Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken ct = default);
    }
}
