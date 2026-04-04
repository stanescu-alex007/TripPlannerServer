using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IUserService
    {
        Task<UserDto?> GetMeAsync(Guid userId);
        Task<Result> UpdateAsync(Guid userId, UpdateUserRequest request);
    }

}