namespace TripPlanner.Core.Common;

/// <summary>
/// Marks an entity whose creation and last modification are stamped automatically
/// by <c>AuditableEntityInterceptor</c>. Never set these by hand.
/// </summary>
/// <remarks>
/// The interceptor hooks <c>SaveChanges</c>. Bulk operations
/// (<c>ExecuteUpdateAsync</c> / <c>ExecuteDeleteAsync</c>) bypass it entirely — if you
/// use those, set <see cref="UpdatedAt"/> explicitly in the same statement.
/// </remarks>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Marks an entity that is never physically deleted. <c>Remove()</c> is intercepted
/// and turned into a <see cref="DeletedAt"/> stamp, and a global query filter keeps
/// deleted rows out of every query unless <c>IgnoreQueryFilters()</c> is used.
/// </summary>
public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
}
