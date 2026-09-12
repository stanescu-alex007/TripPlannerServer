using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IUserService
    {
        Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct = default);

        Task<Result<UserDto>> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken ct = default);

        /// <summary>Soft-deletes the account and revokes every session.</summary>
        Task<Result> DeleteAccountAsync(Guid userId, CancellationToken ct = default);
    }
}
