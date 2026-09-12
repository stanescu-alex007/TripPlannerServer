using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _users;
        private readonly IRefreshTokenRepository _refreshTokens;

        public UserService(IUserRepository users, IRefreshTokenRepository refreshTokens)
        {
            _users = users;
            _refreshTokens = refreshTokens;
        }

        public async Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);

            return user is null
                ? Result<UserDto>.Fail(ErrorCodes.UserNotFound)
                : Result<UserDto>.Ok(MapToDto(user));
        }

        public async Task<Result<UserDto>> UpdateAsync(
            Guid userId, UpdateUserRequest request, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result<UserDto>.Fail(ErrorCodes.UserNotFound);

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

            if (request.ImageUrl is not null)
                user.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim();

            if (request.BirthDate is not null)
                user.BirthDate = DateTime.SpecifyKind(request.BirthDate.Value.Date, DateTimeKind.Utc);

            await _users.UpdateAsync(user, ct);

            // Return the saved state so the client renders what the server actually holds,
            // rather than optimistically echoing what it sent.
            return Result<UserDto>.Ok(MapToDto(user));
        }

        public async Task<Result> DeleteAccountAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result.Fail(ErrorCodes.UserNotFound);

            // Soft delete keeps trips and their history intact while the account
            // disappears from the app; every session dies with it.
            await _users.DeleteAsync(user, ct);
            await _refreshTokens.RevokeAllForUserAsync(userId, RefreshTokenRevokeReason.Logout, ct);

            return Result.Ok();
        }

        private static UserDto MapToDto(User user) => new()
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            ImageUrl = user.ImageUrl,
            BirthDate = user.BirthDate,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAt = user.CreatedAt
        };
    }
}
