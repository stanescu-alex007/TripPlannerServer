using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface IUserRecoveryCodeRepository
    {
        /// <summary>Unused codes for a user, to be checked one by one against the input.</summary>
        Task<List<UserRecoveryCode>> GetUnusedAsync(Guid userId, CancellationToken ct = default);

        Task<int> CountUnusedAsync(Guid userId, CancellationToken ct = default);

        /// <summary>Replaces the whole set — regenerating invalidates every old code.</summary>
        Task ReplaceAllAsync(Guid userId, IEnumerable<UserRecoveryCode> codes, CancellationToken ct = default);

        Task MarkUsedAsync(Guid codeId, CancellationToken ct = default);

        Task DeleteAllAsync(Guid userId, CancellationToken ct = default);
    }
}
