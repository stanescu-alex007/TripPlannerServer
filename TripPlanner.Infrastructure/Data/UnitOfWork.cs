using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TripPlanner.Core.Interfaces;

namespace TripPlanner.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        // Already inside a transaction — a nested call must join the outer one rather
        // than open a second, which would deadlock against it.
        if (_context.Database.CurrentTransaction is not null)
            return await work(ct);

        // The execution strategy owns the retry loop. Opening a transaction outside it
        // would break retries, because a retried operation must replay the whole
        // transaction, not resume a dead one.
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction =
                await _context.Database.BeginTransactionAsync(ct);

            try
            {
                var result = await work(ct);

                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);

                // The change tracker still holds the entities that were rolled back.
                // Leaving them would let a later save in the same request re-apply
                // writes the database has already discarded.
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct = default) =>
        ExecuteAsync<object?>(async token =>
        {
            await work(token);
            return null;
        }, ct);
}
