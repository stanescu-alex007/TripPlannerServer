using Microsoft.Extensions.Logging;
using TripPlanner.BusinessLogic.Email;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services.Auth
{
    public class PasswordManagementService : IPasswordManagementService
    {
        private readonly IUserRepository _users;
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly IPasswordService _passwords;
        private readonly IOneTimeTokenService _oneTimeTokens;
        private readonly ISessionIssuer _sessions;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _email;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<PasswordManagementService> _logger;

        public PasswordManagementService(
            IUserRepository users,
            IRefreshTokenRepository refreshTokens,
            IPasswordService passwords,
            IOneTimeTokenService oneTimeTokens,
            ISessionIssuer sessions,
            IUnitOfWork unitOfWork,
            IEmailSender email,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<PasswordManagementService> logger)
        {
            _users = users;
            _refreshTokens = refreshTokens;
            _passwords = passwords;
            _oneTimeTokens = oneTimeTokens;
            _sessions = sessions;
            _unitOfWork = unitOfWork;
            _email = email;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        private TimeSpan ResetLifetime => TimeSpan.FromMinutes(_settings.PasswordResetMinutes);

        public async Task<Result> ForgotPasswordAsync(string email, CancellationToken ct = default)
        {
            var user = await _users.GetByEmailAsync(EmailAddress.Normalize(email), ct);

            // Still 200 inside the cooldown, so the response reveals nothing about the account.
            if (user is not null
                && !await _oneTimeTokens.IssuedRecentlyAsync(
                    user.Id, UserTokenPurpose.PasswordReset, AuthSettings.EmailResendCooldown, ct))
            {
                var raw = await _oneTimeTokens.IssueAsync(
                    user.Id, UserTokenPurpose.PasswordReset, ResetLifetime, ct: ct);

                await _email.TrySendAsync(
                    AuthEmailTemplates.PasswordReset(
                        user, FrontendLinks.ResetPassword(_settings, raw), _settings.PasswordResetMinutes),
                    _logger, ct);
            }

            return Result.Ok();
        }

        public async Task<Result> ResetPasswordAsync(
            string rawToken, string newPassword, CancellationToken ct = default)
        {
            var found = await _oneTimeTokens.FindUsableAsync(rawToken, UserTokenPurpose.PasswordReset, ct);
            if (!found.Success) return Result.Fail(found.Error!);

            var token = found.Data!;
            var user = token.User;

            var policyError = PasswordPolicy.Validate(
                newPassword, _settings, user.Email, user.FirstName, user.LastName);

            if (policyError is not null) return Result.Fail(policyError);

            // Hashed before the transaction opens: Argon2 is deliberately slow, and there
            // is no reason to hold locks while it runs.
            var hash = _passwords.Hash(newPassword);

            // A reset is the cure for a compromised account. A new password that landed
            // without every session being revoked would leave the intruder signed in, so
            // all of it commits together or none of it does.
            var consumed = await _unitOfWork.ExecuteAsync(async t =>
            {
                if (!await _oneTimeTokens.TryConsumeAsync(token, t)) return false;

                user.PasswordHash = hash;
                user.PasswordChangedAt = Now;
                user.FailedLoginAttempts = 0;
                user.LastFailedLoginAt = null;
                await _users.UpdateAsync(user, t);

                await _refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokeReason.PasswordChanged, t);
                return true;
            }, ct);

            if (!consumed) return Result.Fail(ErrorCodes.TokenAlreadyUsed);

            _logger.LogInformation("Password reset for user {UserId}; all sessions revoked.", user.Id);
            return Result.Ok();
        }

        public async Task<Result<AuthTokens>> ChangePasswordAsync(
            Guid userId, string currentPassword, string newPassword,
            RequestContext context, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result<AuthTokens>.Fail(ErrorCodes.UserNotFound);

            // An account created through an external provider has nothing to compare.
            if (user.PasswordHash is null)
                return Result<AuthTokens>.Fail(ErrorCodes.PasswordNotSet);

            if (!_passwords.Verify(currentPassword, user.PasswordHash))
                return Result<AuthTokens>.Fail(ErrorCodes.CurrentPasswordIncorrect);

            var policyError = PasswordPolicy.Validate(
                newPassword, _settings, user.Email, user.FirstName, user.LastName);

            if (policyError is not null) return Result<AuthTokens>.Fail(policyError);

            if (_passwords.Verify(newPassword, user.PasswordHash))
                return Result<AuthTokens>.Fail(ErrorCodes.PasswordSameAsCurrent);

            var hash = _passwords.Hash(newPassword);

            var tokens = await _unitOfWork.ExecuteAsync(async t =>
            {
                user.PasswordHash = hash;
                user.PasswordChangedAt = Now;
                await _users.UpdateAsync(user, t);

                await _refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokeReason.PasswordChanged, t);
                return await _sessions.StartAsync(user, context, t);
            }, ct);

            // If it was not the real owner who changed it, this notice is their only
            // warning — so it goes out even though the operation succeeded.
            await NotifyPasswordChangedAsync(user, ct);

            _logger.LogInformation("Password changed for user {UserId}; other sessions revoked.", user.Id);
            return Result<AuthTokens>.Ok(tokens);
        }

        /// <summary>
        /// Carries a reset link, so the "this wasn't me" button works without a second
        /// round trip.
        /// </summary>
        private async Task NotifyPasswordChangedAsync(User user, CancellationToken ct)
        {
            var raw = await _oneTimeTokens.IssueAsync(
                user.Id, UserTokenPurpose.PasswordReset, ResetLifetime, ct: ct);

            await _email.TrySendAsync(
                AuthEmailTemplates.PasswordChanged(user, FrontendLinks.ResetPassword(_settings, raw)),
                _logger, ct);
        }
    }
}
