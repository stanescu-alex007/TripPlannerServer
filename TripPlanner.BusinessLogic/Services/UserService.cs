using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _users;
        private readonly ITripRepository _trips;
        private readonly ITripOwnershipService _ownership;
        private readonly IPasswordService _passwords;
        private readonly IUnitOfWork _unitOfWork;

        public UserService(
            IUserRepository users,
            ITripRepository trips,
            ITripOwnershipService ownership,
            IPasswordService passwords,
            IUnitOfWork unitOfWork)
        {
            _users = users;
            _trips = trips;
            _ownership = ownership;
            _passwords = passwords;
            _unitOfWork = unitOfWork;
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

        public async Task<Result> DeleteAccountAsync(
            Guid userId, string password, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result.Fail(ErrorCodes.UserNotFound);

            // Re-asked for the same reason as disabling 2FA or changing the email: an
            // access token lifted from a shared machine or a malicious script must not be
            // enough to erase the account and everything on it. Same checks, same codes.
            if (user.PasswordHash is null) return Result.Fail(ErrorCodes.PasswordNotSet);

            if (!_passwords.Verify(password, user.PasswordHash))
                return Result.Fail(ErrorCodes.CurrentPasswordIncorrect);

            IReadOnlyList<OwnershipOfferNotice> offers;

            try
            {
                offers = await _unitOfWork.ExecuteAsync(async t =>
                {
                    // Owned trips are offered to the next person in line rather than deleted
                    // out from under everyone on them; with nobody left, they are deleted.
                    var created = await _ownership.ReleaseDepartingUserAsync(userId, t);

                    // Seats are vacated, so rosters stop listing someone who can no longer
                    // answer, and the address can be invited afresh.
                    await _trips.RemoveUserFromAllTripsAsync(userId, t);

                    // Sessions and every other credential are purged along with the account.
                    await _users.DeleteAsync(user, t);

                    return created;
                }, ct);
            }
            catch (ConcurrencyConflictException)
            {
                // An ownership offer involving this user was answered or lapsed at the
                // same instant. Everything rolled back — the account is intact — and a
                // retry works from the offer's new state.
                return Result.Fail(ErrorCodes.ConcurrencyConflict);
            }

            // Only once the deletion has committed — never mail an offer that rolled back.
            await _ownership.NotifyCandidatesAsync(offers, ct);

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
            IsTwoFactorEnabled = user.IsTwoFactorEnabled,
            CreatedAt = user.CreatedAt
        };
    }
}
