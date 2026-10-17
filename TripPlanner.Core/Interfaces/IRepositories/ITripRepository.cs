using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface ITripRepository
    {
        /// <summary>
        /// Every trip the user can see — owned <em>and</em> ones they were invited to.
        /// The old query filtered on OwnerId only, so trips you were a participant on
        /// never appeared anywhere in the app.
        /// </summary>
        Task<List<Trip>> GetAccessibleByUserAsync(Guid userId, CancellationToken ct = default);

        Task<Trip?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>Includes participants — needed by every access check.</summary>
        Task<Trip?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

        Task<Trip> CreateAsync(Trip trip, CancellationToken ct = default);

        Task UpdateAsync(Trip trip, CancellationToken ct = default);

        /// <summary>
        /// Soft-deletes the trip together with its participants and schedule.
        /// <paramref name="deletedByUserId"/> is whose deleted list it lands in; null when
        /// nobody deleted it deliberately and it is simply waiting to be purged.
        /// </summary>
        Task DeleteAsync(Guid tripId, Guid? deletedByUserId, CancellationToken ct = default);

        /// <summary>Trips the user deleted and can still restore.</summary>
        Task<List<Trip>> GetDeletedByUserAsync(Guid userId, DateTime since, CancellationToken ct = default);

        /// <summary>A deleted trip, which no ordinary query would return.</summary>
        Task<Trip?> GetDeletedByIdAsync(Guid tripId, CancellationToken ct = default);

        /// <summary>
        /// Brings the trip back, along with everyone and everything that went down with
        /// it — but not participants removed before that, who left for their own reasons.
        /// </summary>
        Task RestoreAsync(Trip trip, CancellationToken ct = default);

        /// <summary>Physically removes trips deleted before the cutoff. Returns how many.</summary>
        Task<int> PurgeDeletedBeforeAsync(DateTime cutoff, CancellationToken ct = default);

        /// <summary>
        /// Every live trip the user owns or holds a seat on — declined seats included —
        /// with its roster and schedule, for the personal data export.
        /// </summary>
        Task<List<Trip>> GetForExportAsync(Guid userId, CancellationToken ct = default);

        /// <summary>Soft-deletes the user's seat on every trip they belong to.</summary>
        Task RemoveUserFromAllTripsAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Moves the user's participant rows to their new address.
        ///
        /// <para>
        /// Rows already addressed to <paramref name="newEmail"/> are resolved first: on a
        /// trip where the user already has a seat the extra row is removed, since it
        /// would otherwise collide with the moved one on the (TripId, Email) index;
        /// elsewhere an unclaimed invitation to that address is linked to the user, who
        /// has just proved they own it.
        /// </para>
        /// </summary>
        Task ReassignParticipantEmailAsync(Guid userId, string newEmail, CancellationToken ct = default);

        /// <summary>
        /// Throws <see cref="Common.DuplicateParticipantException"/> when the unique index
        /// on (TripId, Email) rejects the insert.
        /// </summary>
        Task AddParticipantAsync(TripParticipant participant, CancellationToken ct = default);

        Task UpdateParticipantAsync(TripParticipant participant, CancellationToken ct = default);

        Task RemoveParticipantAsync(Guid participantId, CancellationToken ct = default);

        /// <summary>Expects an already-normalized address.</summary>
        Task<TripParticipant?> GetParticipantByEmailAsync(Guid tripId, string email, CancellationToken ct = default);

        /// <summary>One participant with its trip and owner loaded, for invitation emails.</summary>
        Task<TripParticipant?> GetParticipantWithTripAsync(Guid participantId, CancellationToken ct = default);

        /// <summary>
        /// Binds participant rows invited by email to an account that has just been
        /// created, so an invitation sent before sign-up is not orphaned.
        /// </summary>
        Task<int> LinkParticipantsByEmailAsync(string email, Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Replaces the non-owner roster in one transaction, preserving the Status of
        /// participants that survive the edit.
        /// </summary>
        Task SyncParticipantsAsync(Guid tripId, IReadOnlyCollection<TripParticipant> desired, CancellationToken ct = default);
    }
}
