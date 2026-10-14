using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TripPlanner.Core.Common;

namespace TripPlanner.Infrastructure.Data.Interceptors;

/// <summary>
/// Stamps <see cref="IAuditable"/> timestamps and turns deletes of
/// <see cref="ISoftDeletable"/> entities into <c>DeletedAt</c> updates.
///
/// <para>
/// This runs on <c>SaveChanges</c> only. Bulk operations — <c>ExecuteUpdateAsync</c>
/// and <c>ExecuteDeleteAsync</c> — never reach the change tracker and therefore never
/// reach this interceptor; anything using them must maintain the columns itself.
/// </para>
///
/// <para>
/// Soft-deleting a parent does not cascade: the rows are no longer deleted, so the
/// database's FK cascade never fires. Children that must disappear with their parent
/// have to be marked explicitly (see <c>TripRepository.DeleteAsync</c>).
/// </para>
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _clock;

    public AuditableEntityInterceptor(TimeProvider clock) => _clock = clock;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = _clock.GetUtcNow().UtcDateTime;

        // Materialised because the loop changes entry states.
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            // Deleting something soft-deletable becomes an update.
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable)
            {
                SoftDelete(entry, now);
                continue;
            }

            if (entry.Entity is not IAuditable auditable)
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAt = now;
                    auditable.UpdatedAt = null;
                    break;

                case EntityState.Modified:
                    auditable.UpdatedAt = now;
                    // CreatedAt is immutable once written.
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }

    /// <summary>
    /// Writes only the deletion columns. The order matters: clearing <c>IsModified</c>
    /// restores a property's original value, so the stamps are assigned after the
    /// entry has been narrowed — assigned before, they were silently reverted and the
    /// row was never actually marked deleted.
    /// </summary>
    private static void SoftDelete(EntityEntry entry, DateTime now)
    {
        entry.State = EntityState.Modified;

        foreach (var property in entry.Properties)
            property.IsModified = false;

        Stamp(entry, nameof(ISoftDeletable.DeletedAt), now);

        if (entry.Entity is IAuditable)
            Stamp(entry, nameof(IAuditable.UpdatedAt), now);
    }

    private static void Stamp(EntityEntry entry, string propertyName, DateTime value)
    {
        var property = entry.Property(propertyName);
        property.CurrentValue = value;
        property.IsModified = true;
    }
}
