namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Reacts to an account finishing email verification.
    ///
    /// <para>
    /// Authentication has no business knowing what else in the system cares about a
    /// new account. It previously called the invitation service directly, which meant
    /// auth depended on trips — a dependency pointing the wrong way, and one that
    /// would have grown with every feature that wants to know about new users.
    /// </para>
    ///
    /// <para>
    /// Handlers are resolved as a collection and are expected not to throw: a failure
    /// in a follow-up action must not undo a verification that already succeeded.
    /// </para>
    /// </summary>
    public interface IUserVerifiedHandler
    {
        Task HandleAsync(Guid userId, string email, CancellationToken ct = default);
    }
}
