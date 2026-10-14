namespace TripPlanner.Core.Interfaces
{
    /// <summary>
    /// Runs several repository calls as one database transaction.
    ///
    /// <para>
    /// Repositories each save their own work, which is fine for a single change but
    /// leaves multi-step operations partially applied when one step fails. Creating a
    /// trip, for example, writes the trip, then the owner's participant row, then the
    /// invitees — three commits, and a failure after the first produced a trip with no
    /// participants at all.
    /// </para>
    ///
    /// <para>
    /// Wrapping the work here opens one transaction up front; the repositories' own
    /// saves enlist in it instead of committing independently, so the whole operation
    /// either lands or does not.
    /// </para>
    /// </summary>
    public interface IUnitOfWork
    {
        Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct = default);

        Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
    }
}
