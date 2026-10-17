using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IUserService
    {
        Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct = default);

        Task<Result<UserDto>> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken ct = default);

        /// <summary>
        /// Soft-deletes the account and revokes every session. Requires the current
        /// password, like every other action that cannot be undone or weakens the
        /// account: a hijacked session alone must not be enough to destroy it.
        /// </summary>
        Task<Result> DeleteAccountAsync(Guid userId, string password, CancellationToken ct = default);

        /// <summary>
        /// Everything stored about the user, in a portable form (GDPR right of access
        /// and portability). Password-confirmed, since it hands over the whole account.
        /// </summary>
        Task<Result<UserDataExport>> ExportAsync(Guid userId, string password, CancellationToken ct = default);
    }
}
