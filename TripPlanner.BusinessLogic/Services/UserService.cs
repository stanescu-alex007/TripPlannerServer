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
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly IImageProcessor _imageProcessor;
        private readonly IImageStorage _imageStorage;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        public UserService(
            IUserRepository users,
            ITripRepository trips,
            ITripOwnershipService ownership,
            IPasswordService passwords,
            IRefreshTokenRepository refreshTokens,
            IImageProcessor imageProcessor,
            IImageStorage imageStorage,
            IUnitOfWork unitOfWork,
            TimeProvider clock)
        {
            _users = users;
            _trips = trips;
            _ownership = ownership;
            _passwords = passwords;
            _refreshTokens = refreshTokens;
            _imageProcessor = imageProcessor;
            _imageStorage = imageStorage;
            _unitOfWork = unitOfWork;
            _clock = clock;
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

            if (request.BirthDate is not null)
                user.BirthDate = DateTime.SpecifyKind(request.BirthDate.Value.Date, DateTimeKind.Utc);

            await _users.UpdateAsync(user, ct);

            // Return the saved state so the client renders what the server actually holds,
            // rather than optimistically echoing what it sent.
            return Result<UserDto>.Ok(MapToDto(user));
        }

        public async Task<Result<UserDto>> SetPhotoAsync(
            Guid userId, Stream file, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result<UserDto>.Fail(ErrorCodes.UserNotFound);

            var processed = await _imageProcessor.ProcessAsync(file, ImageKind.Avatar, ct);
            if (!processed.Success) return Result<UserDto>.Fail(processed.Error!);

            var previous = user.ImageUrl;
            var url = await _imageStorage.SaveAsync(ImageFolders.Avatars, processed.Data!, ct);

            user.ImageUrl = url;

            try
            {
                await _users.UpdateAsync(user, ct);
            }
            catch
            {
                // Nothing points at the new file, so it must not linger.
                await _imageStorage.DeleteAsync(url, CancellationToken.None);
                throw;
            }

            // Only once the new URL is saved — the old photo stays until nothing uses it.
            await _imageStorage.DeleteAsync(previous, ct);

            return Result<UserDto>.Ok(MapToDto(user));
        }

        public async Task<Result<UserDto>> RemovePhotoAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result<UserDto>.Fail(ErrorCodes.UserNotFound);

            var previous = user.ImageUrl;
            if (previous is null) return Result<UserDto>.Ok(MapToDto(user));

            user.ImageUrl = null;
            await _users.UpdateAsync(user, ct);
            await _imageStorage.DeleteAsync(previous, ct);

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

            // Read before the delete, which anonymizes the row and clears it.
            var photo = user.ImageUrl;

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

            // The photo is personal data like the rest; erased with the account.
            await _imageStorage.DeleteAsync(photo, ct);

            return Result.Ok();
        }

        public async Task<Result<UserDataExport>> ExportAsync(
            Guid userId, string password, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result<UserDataExport>.Fail(ErrorCodes.UserNotFound);

            // The whole account in one file: a lifted access token must not be enough.
            if (user.PasswordHash is null) return Result<UserDataExport>.Fail(ErrorCodes.PasswordNotSet);

            if (!_passwords.Verify(password, user.PasswordHash))
                return Result<UserDataExport>.Fail(ErrorCodes.CurrentPasswordIncorrect);

            var trips = await _trips.GetForExportAsync(userId, ct);
            var sessions = await _refreshTokens.GetActiveSessionsAsync(userId, ct);

            return Result<UserDataExport>.Ok(new UserDataExport
            {
                ExportedAt = _clock.GetUtcNow().UtcDateTime,
                Profile = new ExportedProfile
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Phone = user.Phone,
                    ImageUrl = user.ImageUrl,
                    BirthDate = user.BirthDate,
                    CreatedAt = user.CreatedAt,
                    EmailVerifiedAt = user.EmailVerifiedAt,
                    TermsAcceptedAt = user.TermsAcceptedAt,
                    PasswordChangedAt = user.PasswordChangedAt,
                    TwoFactorEnabledAt = user.TwoFactorEnabledAt
                },
                Trips = trips.Select(t => MapToExport(t, userId)).ToList(),
                ActiveSessions = sessions.Select(s => new ExportedSession
                {
                    IpAddress = s.CreatedByIp,
                    UserAgent = s.UserAgent,
                    CreatedAt = s.CreatedAt,
                    ExpiresAt = s.ExpiresAt
                }).ToList()
            });
        }

        private static ExportedTrip MapToExport(Trip trip, Guid userId)
        {
            var seat = trip.Participants.FirstOrDefault(p => p.UserId == userId);

            return new ExportedTrip
            {
                Id = trip.Id,
                Name = trip.Name,
                Location = trip.Location,
                StartDate = trip.StartDate,
                EndDate = trip.EndDate,
                Budget = trip.Budget,
                Currency = trip.Currency,
                TransportType = trip.TransportType,
                Description = trip.Description,
                Image = trip.Image,
                CreatedAt = trip.CreatedAt,
                IsOwner = trip.OwnerId == userId,
                MyNickname = seat?.Nickname,
                MyRole = seat?.Role,
                MyStatus = seat?.Status,
                OtherParticipants = trip.Participants
                    .Where(p => p.UserId != userId)
                    .Select(p => p.Nickname)
                    .ToList(),
                Schedule = trip.ScheduleItems
                    .OrderBy(s => s.Date).ThenBy(s => s.Order)
                    .Select(s => new ExportedScheduleItem
                    {
                        Date = s.Date,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Title = s.Title,
                        Description = s.Description,
                        Status = s.Status
                    })
                    .ToList()
            };
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
