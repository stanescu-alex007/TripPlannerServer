namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Reacts to an account's email address changing, for data elsewhere that is keyed
    /// by the address.
    ///
    /// <para>
    /// Unlike <see cref="IUserVerifiedHandler"/>, handlers run inside the same
    /// transaction as the change and are expected to throw when they fail. Rows left
    /// holding the old address are precisely the inconsistency this exists to prevent,
    /// so a failed handler must roll the change back rather than let it stand half done.
    /// </para>
    /// </summary>
    public interface IUserEmailChangedHandler
    {
        Task HandleAsync(Guid userId, string previousEmail, string newEmail, CancellationToken ct = default);
    }
}
