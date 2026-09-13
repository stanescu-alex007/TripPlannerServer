using Microsoft.Extensions.Logging;
using TripPlanner.BusinessLogic.Email;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _users;
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly IUserTokenRepository _userTokens;
        private readonly IUserRecoveryCodeRepository _recoveryCodes;
        private readonly IPasswordService _passwords;
        private readonly IJwtService _jwt;
        private readonly ISecureTokenService _secureTokens;
        private readonly ITwoFactorService _twoFactor;
        private readonly IEmailSender _email;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository users,
            IRefreshTokenRepository refreshTokens,
            IUserTokenRepository userTokens,
            IUserRecoveryCodeRepository recoveryCodes,
            IPasswordService passwords,
            IJwtService jwt,
            ISecureTokenService secureTokens,
            ITwoFactorService twoFactor,
            IEmailSender email,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<AuthService> logger)
        {
            _users = users;
            _refreshTokens = refreshTokens;
            _userTokens = userTokens;
            _recoveryCodes = recoveryCodes;
            _passwords = passwords;
            _jwt = jwt;
            _secureTokens = secureTokens;
            _twoFactor = twoFactor;
            _email = email;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // ── REGISTER ──────────────────────────────────────────────────────────

        public async Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var email = Normalize(request.Email);

            var policyError = PasswordPolicy.Validate(
                request.Password, _settings, email, request.FirstName, request.LastName);

            if (policyError is not null) return Result.Fail(policyError);

            var existing = await _users.GetByEmailAsync(email, ct);

            if (existing is not null)
            {
                // An account that was never confirmed is not really taken. Letting the
                // address stay claimed forever means one typo at registration locks the
                // real owner out permanently, with no way to recover it.
                if (existing.IsEmailVerified)
                    return Result.Fail(ErrorCodes.EmailAlreadyRegistered);

                existing.FirstName = request.FirstName.Trim();
                existing.LastName = request.LastName.Trim();
                existing.BirthDate = ToUtcDate(request.BirthDate);
                existing.PasswordHash = _passwords.Hash(request.Password);

                await _users.UpdateAsync(existing, ct);
                await SendVerificationEmailAsync(existing, ct);

                _logger.LogInformation(
                    "Registration replaced the unconfirmed account {UserId}.", existing.Id);

                return Result.Ok();
            }

            var user = new User
            {
                Email = email,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                BirthDate = ToUtcDate(request.BirthDate),
                PasswordHash = _passwords.Hash(request.Password),
                EmailVerifiedAt = null
            };

            try
            {
                await _users.AddAsync(user, ct);
            }
            catch (DuplicateEmailException)
            {
                // Lost a race with a concurrent registration; the unique index decided.
                return Result.Fail(ErrorCodes.EmailAlreadyRegistered);
            }

            await SendVerificationEmailAsync(user, ct);
            return Result.Ok();
        }

        // ── EMAIL VERIFICATION ────────────────────────────────────────────────

        public async Task<Result> VerifyEmailAsync(string rawToken, CancellationToken ct = default)
        {
            var (token, error) = await RedeemTokenAsync(rawToken, UserTokenPurpose.EmailVerification, ct);
            if (error is not null) return Result.Fail(error);

            var user = token!.User;

            if (user.IsEmailVerified)
                return Result.Fail(ErrorCodes.EmailAlreadyVerified);

            user.EmailVerifiedAt = Now;
            await _users.UpdateAsync(user, ct);

            token.UsedAt = Now;
            await _userTokens.UpdateAsync(token, ct);

            _logger.LogInformation("Email verified for user {UserId}.", user.Id);
            return Result.Ok();
        }

        public async Task<Result> ResendVerificationAsync(string email, CancellationToken ct = default)
        {
            var user = await _users.GetByEmailAsync(Normalize(email), ct);

            if (user is not null && !user.IsEmailVerified)
                await SendVerificationEmailAsync(user, ct);

            return Result.Ok();
        }

        // ── LOGIN ─────────────────────────────────────────────────────────────

        public async Task<Result<LoginOutcome>> LoginAsync(
            LoginRequest request, RequestContext context, CancellationToken ct = default)
        {
            var user = await _users.GetByEmailAsync(Normalize(request.Email), ct);

            // Per-account throttling, checked before the password: IP rate limiting is
            // useless against an attacker who rotates addresses against one account.
            if (user is not null && RemainingDelay(user) > TimeSpan.Zero)
            {
                _logger.LogWarning("Throttled sign-in attempt for user {UserId}.", user.Id);
                return Result<LoginOutcome>.Fail(ErrorCodes.LoginThrottled);
            }

            // Hash even when the account is missing or passwordless, so the response
            // time does not reveal which addresses exist.
            var passwordValid = user?.PasswordHash is not null
                ? _passwords.Verify(request.Password, user.PasswordHash)
                : _passwords.VerifyDummy(request.Password);

            if (user is null || !passwordValid)
            {
                if (user is not null) await RecordFailureAsync(user, ct);
                return Result<LoginOutcome>.Fail(ErrorCodes.InvalidCredentials);
            }

            if (!user.IsEmailVerified)
                return Result<LoginOutcome>.Fail(ErrorCodes.EmailNotVerified);

            await ClearFailuresAsync(user, ct);

            if (user.IsTwoFactorEnabled)
            {
                var challenge = await IssueTwoFactorChallengeAsync(user, ct);
                return Result<LoginOutcome>.Ok(LoginOutcome.NeedsTwoFactor(challenge));
            }

            var tokens = await StartNewFamilyAsync(user, context, ct);

            _logger.LogInformation("User {UserId} signed in.", user.Id);
            return Result<LoginOutcome>.Ok(LoginOutcome.SignedIn(tokens));
        }

        // ── TWO-FACTOR AT SIGN-IN ─────────────────────────────────────────────

        public async Task<Result<AuthTokens>> CompleteTwoFactorAsync(
            string challengeToken, string code, RequestContext context, CancellationToken ct = default)
        {
            var (token, error) = await RedeemTokenAsync(
                challengeToken, UserTokenPurpose.TwoFactorChallenge, ct);

            if (error is not null)
                return Result<AuthTokens>.Fail(ErrorCodes.TwoFactorChallengeInvalid);

            var user = token!.User;

            if (!user.IsTwoFactorEnabled || user.TwoFactorSecret is null)
                return Result<AuthTokens>.Fail(ErrorCodes.TwoFactorNotEnabled);

            var accepted = _twoFactor.VerifyCode(user.TwoFactorSecret, code)
                           || await TryConsumeRecoveryCodeAsync(user, code, ct);

            if (!accepted)
            {
                // A wrong code counts as a failed attempt: without this, 2FA could be
                // brute-forced freely once the password was known.
                await RecordFailureAsync(user, ct);
                return Result<AuthTokens>.Fail(ErrorCodes.TwoFactorCodeInvalid);
            }

            // Burn the challenge only once it actually succeeded, so a mistyped code
            // does not force the user back to the password screen.
            token.UsedAt = Now;
            await _userTokens.UpdateAsync(token, ct);

            await ClearFailuresAsync(user, ct);

            var tokens = await StartNewFamilyAsync(user, context, ct);

            _logger.LogInformation("User {UserId} completed two-factor sign-in.", user.Id);
            return Result<AuthTokens>.Ok(tokens);
        }

        // ── REFRESH ───────────────────────────────────────────────────────────

        public async Task<Result<AuthTokens>> RefreshAsync(
            string rawRefreshToken, RequestContext context, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenMissing);

            var hash = _secureTokens.Hash(rawRefreshToken);
            var stored = await _refreshTokens.GetByHashAsync(hash, ct);

            if (stored is null)
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenInvalid);

            // Replay of an already-rotated link: the token leaked, so the entire family
            // can no longer be trusted.
            if (stored.RevokedAt is not null)
            {
                var revoked = await _refreshTokens.RevokeFamilyAsync(
                    stored.FamilyId, RefreshTokenRevokeReason.ReuseDetected, ct);

                _logger.LogWarning(
                    "Refresh token replay for user {UserId}; detonated family {FamilyId} ({Count} revoked).",
                    stored.UserId, stored.FamilyId, revoked);

                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenReused);
            }

            var now = Now;

            if (stored.ExpiresAt <= now)
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenExpired);

            if (stored.FamilyExpiresAt <= now)
            {
                await _refreshTokens.RevokeFamilyAsync(
                    stored.FamilyId, RefreshTokenRevokeReason.Rotated, ct);
                return Result<AuthTokens>.Fail(ErrorCodes.SessionExpired);
            }

            var user = await _users.GetByIdAsync(stored.UserId, ct);
            if (user is null)
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenInvalid);

            if (!user.IsEmailVerified)
                return Result<AuthTokens>.Fail(ErrorCodes.EmailNotVerified);

            var raw = _secureTokens.Generate();

            var replacement = new RefreshToken
            {
                UserId = user.Id,
                FamilyId = stored.FamilyId,
                TokenHash = _secureTokens.Hash(raw),
                ExpiresAt = now.AddDays(_settings.RefreshTokenDays),
                FamilyExpiresAt = stored.FamilyExpiresAt,
                CreatedByIp = context.IpAddress,
                UserAgent = context.UserAgent
            };

            await _refreshTokens.RotateAsync(stored, replacement, ct);

            return Result<AuthTokens>.Ok(
                BuildTokens(user, stored.FamilyId, raw, replacement.ExpiresAt));
        }

        // ── LOGOUT ────────────────────────────────────────────────────────────

        public async Task<Result> LogoutAsync(string? rawRefreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return Result.Ok();

            var stored = await _refreshTokens.GetByHashAsync(_secureTokens.Hash(rawRefreshToken), ct);

            if (stored is not null)
            {
                await _refreshTokens.RevokeFamilyAsync(
                    stored.FamilyId, RefreshTokenRevokeReason.Logout, ct);

                _logger.LogInformation("User {UserId} signed out.", stored.UserId);
            }

            return Result.Ok();
        }

        // ── FORGOT / RESET PASSWORD ───────────────────────────────────────────

        public async Task<Result> ForgotPasswordAsync(string email, CancellationToken ct = default)
        {
            var user = await _users.GetByEmailAsync(Normalize(email), ct);

            if (user is not null)
            {
                await _userTokens.InvalidateOutstandingAsync(user.Id, UserTokenPurpose.PasswordReset, ct);

                var raw = _secureTokens.Generate();

                await _userTokens.AddAsync(new UserToken
                {
                    UserId = user.Id,
                    TokenHash = _secureTokens.Hash(raw),
                    Purpose = UserTokenPurpose.PasswordReset,
                    ExpiresAt = Now.AddMinutes(_settings.PasswordResetMinutes)
                }, ct);

                await TrySendAsync(AuthEmailTemplates.PasswordReset(
                    user, ResetLink(raw), _settings.PasswordResetMinutes), ct);
            }

            return Result.Ok();
        }

        public async Task<Result> ResetPasswordAsync(
            string rawToken, string newPassword, CancellationToken ct = default)
        {
            var (token, tokenError) = await RedeemTokenAsync(rawToken, UserTokenPurpose.PasswordReset, ct);
            if (tokenError is not null) return Result.Fail(tokenError);

            var user = token!.User;

            var policyError = PasswordPolicy.Validate(
                newPassword, _settings, user.Email, user.FirstName, user.LastName);

            if (policyError is not null) return Result.Fail(policyError);

            user.PasswordHash = _passwords.Hash(newPassword);
            user.PasswordChangedAt = Now;

            // A reset is the cure for a compromised account, so every session dies —
            // including whoever forced the reset.
            user.FailedLoginAttempts = 0;
            user.LastFailedLoginAt = null;

            await _users.UpdateAsync(user, ct);
            await _refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokeReason.PasswordChanged, ct);

            token.UsedAt = Now;
            await _userTokens.UpdateAsync(token, ct);

            _logger.LogInformation("Password reset for user {UserId}; all sessions revoked.", user.Id);
            return Result.Ok();
        }

        // ── CHANGE PASSWORD ───────────────────────────────────────────────────

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

            user.PasswordHash = _passwords.Hash(newPassword);
            user.PasswordChangedAt = Now;
            await _users.UpdateAsync(user, ct);

            await _refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokeReason.PasswordChanged, ct);
            var tokens = await StartNewFamilyAsync(user, context, ct);

            // If it was not the real owner who changed it, this notice is their only
            // warning — so it is sent even though the operation succeeded.
            await NotifyPasswordChangedAsync(user, ct);

            _logger.LogInformation("Password changed for user {UserId}; other sessions revoked.", user.Id);
            return Result<AuthTokens>.Ok(tokens);
        }

        // ── Throttling ────────────────────────────────────────────────────────

        /// <summary>
        /// How long is still owed before the next attempt is allowed. Doubles per
        /// failure past the threshold, capped — a delay rather than a lockout, so an
        /// attacker cannot deliberately shut a user out of their own account.
        /// </summary>
        private TimeSpan RemainingDelay(User user)
        {
            if (user.FailedLoginAttempts < _settings.LoginFailuresBeforeDelay) return TimeSpan.Zero;
            if (user.LastFailedLoginAt is null) return TimeSpan.Zero;

            var over = user.FailedLoginAttempts - _settings.LoginFailuresBeforeDelay;

            // Exponent capped before shifting, so the multiplier cannot overflow.
            var multiplier = Math.Pow(2, Math.Min(over, 16));
            var seconds = Math.Min(_settings.LoginDelayBaseSeconds * multiplier, _settings.LoginDelayMaxSeconds);

            var readyAt = user.LastFailedLoginAt.Value.AddSeconds(seconds);
            var remaining = readyAt - Now;

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        private async Task RecordFailureAsync(User user, CancellationToken ct)
        {
            user.FailedLoginAttempts++;
            user.LastFailedLoginAt = Now;
            await _users.UpdateAsync(user, ct);
        }

        private async Task ClearFailuresAsync(User user, CancellationToken ct)
        {
            if (user.FailedLoginAttempts == 0 && user.LastFailedLoginAt is null) return;

            user.FailedLoginAttempts = 0;
            user.LastFailedLoginAt = null;
            await _users.UpdateAsync(user, ct);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string Normalize(string email) => email.Trim().ToLowerInvariant();

        private static DateTime? ToUtcDate(DateTime? value) =>
            value is null ? null : DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);

        private string ResetLink(string rawToken) =>
            $"{_settings.FrontendBaseUrl.TrimEnd('/')}/auth/reset-password?token={rawToken}";

        private async Task<bool> TryConsumeRecoveryCodeAsync(User user, string code, CancellationToken ct)
        {
            var normalized = code.Trim().ToLowerInvariant().Replace(" ", string.Empty);
            if (normalized.Length < 4) return false;

            // Hashes are salted per code, so each candidate has to be checked in turn
            // rather than looked up.
            foreach (var candidate in await _recoveryCodes.GetUnusedAsync(user.Id, ct))
            {
                if (!_passwords.Verify(normalized, candidate.CodeHash)) continue;

                await _recoveryCodes.MarkUsedAsync(candidate.Id, ct);

                var left = await _recoveryCodes.CountUnusedAsync(user.Id, ct);
                _logger.LogWarning(
                    "User {UserId} signed in with a recovery code; {Remaining} left.", user.Id, left);

                return true;
            }

            return false;
        }

        private async Task<string> IssueTwoFactorChallengeAsync(User user, CancellationToken ct)
        {
            await _userTokens.InvalidateOutstandingAsync(user.Id, UserTokenPurpose.TwoFactorChallenge, ct);

            var raw = _secureTokens.Generate();

            await _userTokens.AddAsync(new UserToken
            {
                UserId = user.Id,
                TokenHash = _secureTokens.Hash(raw),
                Purpose = UserTokenPurpose.TwoFactorChallenge,
                ExpiresAt = Now.AddMinutes(_settings.TwoFactorChallengeMinutes)
            }, ct);

            return raw;
        }

        private async Task<AuthTokens> StartNewFamilyAsync(
            User user, RequestContext context, CancellationToken ct)
        {
            var now = Now;
            var raw = _secureTokens.Generate();

            var token = new RefreshToken
            {
                UserId = user.Id,
                FamilyId = Guid.NewGuid(),
                TokenHash = _secureTokens.Hash(raw),
                ExpiresAt = now.AddDays(_settings.RefreshTokenDays),
                FamilyExpiresAt = now.AddDays(_settings.RefreshFamilyDays),
                CreatedByIp = context.IpAddress,
                UserAgent = context.UserAgent
            };

            await _refreshTokens.AddAsync(token, ct);

            return BuildTokens(user, token.FamilyId, raw, token.ExpiresAt);
        }

        private AuthTokens BuildTokens(
            User user, Guid sessionId, string rawRefreshToken, DateTime refreshExpiresAt) =>
            new(
                AccessToken: _jwt.GenerateToken(user, sessionId),
                AccessTokenExpiresAt: Now.AddMinutes(_settings.AccessTokenMinutes),
                RefreshToken: rawRefreshToken,
                RefreshTokenExpiresAt: refreshExpiresAt);

        private async Task<(UserToken? Token, Error? Error)> RedeemTokenAsync(
            string rawToken, string purpose, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                return (null, ErrorCodes.TokenInvalid);

            var token = await _userTokens.GetByHashAsync(_secureTokens.Hash(rawToken), ct);

            if (token is null || token.Purpose != purpose)
                return (null, ErrorCodes.TokenInvalid);

            if (token.UsedAt is not null) return (null, ErrorCodes.TokenAlreadyUsed);
            if (token.InvalidatedAt is not null) return (null, ErrorCodes.TokenInvalid);
            if (token.ExpiresAt <= Now) return (null, ErrorCodes.TokenExpired);

            return (token, null);
        }

        private async Task SendVerificationEmailAsync(User user, CancellationToken ct)
        {
            await _userTokens.InvalidateOutstandingAsync(user.Id, UserTokenPurpose.EmailVerification, ct);

            var raw = _secureTokens.Generate();

            await _userTokens.AddAsync(new UserToken
            {
                UserId = user.Id,
                TokenHash = _secureTokens.Hash(raw),
                Purpose = UserTokenPurpose.EmailVerification,
                ExpiresAt = Now.AddHours(_settings.EmailVerificationHours)
            }, ct);

            var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/auth/verify-email?token={raw}";

            await TrySendAsync(
                AuthEmailTemplates.VerifyEmail(user, link, _settings.EmailVerificationHours), ct);
        }

        /// <summary>
        /// Issues a reset token purely so the "this wasn't me" button in the
        /// notification works without a second round trip.
        /// </summary>
        private async Task NotifyPasswordChangedAsync(User user, CancellationToken ct)
        {
            var raw = _secureTokens.Generate();

            await _userTokens.AddAsync(new UserToken
            {
                UserId = user.Id,
                TokenHash = _secureTokens.Hash(raw),
                Purpose = UserTokenPurpose.PasswordReset,
                ExpiresAt = Now.AddMinutes(_settings.PasswordResetMinutes)
            }, ct);

            await TrySendAsync(AuthEmailTemplates.PasswordChanged(user, ResetLink(raw)), ct);
        }

        /// <summary>
        /// Sends without letting a mail failure fail the operation: the account change
        /// has already been written, so reporting failure would be a lie.
        /// </summary>
        private async Task TrySendAsync(EmailMessage message, CancellationToken ct)
        {
            try
            {
                await _email.SendAsync(message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Could not deliver '{Subject}' to {Recipient}. The operation itself succeeded.",
                    message.Subject, message.ToAddress);
            }
        }
    }
}
