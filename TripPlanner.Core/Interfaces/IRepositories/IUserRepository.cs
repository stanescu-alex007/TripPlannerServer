using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>Expects an already-normalized (trimmed, lower-cased) address.</summary>
        Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

        Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Throws <see cref="Common.DuplicateEmailException"/> when the unique index
        /// rejects the insert.
        /// </summary>
        Task AddAsync(User user, CancellationToken ct = default);

        Task UpdateAsync(User user, CancellationToken ct = default);

        /// <summary>Soft delete — the row stays, with <c>DeletedAt</c> stamped.</summary>
        Task DeleteAsync(User user, CancellationToken ct = default);
    }
}
