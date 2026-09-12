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

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            // Deleting something soft-deletable becomes an update.
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletable softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.DeletedAt = now;

                // Only the two columns we touched should be written.
                MarkOnly(entry, nameof(ISoftDeletable.DeletedAt), nameof(IAuditable.UpdatedAt));

                if (entry.Entity is IAuditable softAuditable)
                    softAuditable.UpdatedAt = now;

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

    /// <summary>Narrows an entry so only the named properties are sent to the database.</summary>
    private static void MarkOnly(EntityEntry entry, params string[] propertyNames)
    {
        foreach (var property in entry.Properties)
            property.IsModified = false;

        foreach (var name in propertyNames)
        {
            var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == name);
            if (property is not null) property.IsModified = true;
        }
    }
}
