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
        private readonly IPasswordService _passwords;
        private readonly IJwtService _jwt;
        private readonly ISecureTokenService _secureTokens;
        private readonly IEmailSender _email;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository users,
            IRefreshTokenRepository refreshTokens,
            IUserTokenRepository userTokens,
            IPasswordService passwords,
            IJwtService jwt,
            ISecureTokenService secureTokens,
            IEmailSender email,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<AuthService> logger)
        {
            _users = users;
            _refreshTokens = refreshTokens;
            _userTokens = userTokens;
            _passwords = passwords;
            _jwt = jwt;
            _secureTokens = secureTokens;
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

            var policyError = ValidatePassword(request.Password);
            if (policyError is not null) return Result.Fail(policyError);

            if (await _users.ExistsByEmailAsync(email, ct))
                return Result.Fail(ErrorCodes.EmailAlreadyRegistered);

            var user = new User
            {
                Email = email,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                BirthDate = request.BirthDate is null
                    ? null
                    : DateTime.SpecifyKind(request.BirthDate.Value.Date, DateTimeKind.Utc),
                PasswordHash = _passwords.Hash(request.Password),
                EmailVerifiedAt = null
            };

            try
            {
                await _users.AddAsync(user, ct);
            }
            catch (DuplicateEmailException)
            {
                // Two concurrent registrations for the same address — the unique index
                // decides the winner, and the loser reports the same conflict it would
                // have got from the pre-check.
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

            // Silent no-op when the address is unknown or already verified: the response
            // must look identical either way.
            if (user is not null && !user.IsEmailVerified)
                await SendVerificationEmailAsync(user, ct);

            return Result.Ok();
        }

        // ── LOGIN ─────────────────────────────────────────────────────────────

        public async Task<Result<AuthTokens>> LoginAsync(
            LoginRequest request, RequestContext context, CancellationToken ct = default)
        {
            var user = await _users.GetByEmailAsync(Normalize(request.Email), ct);

            // Hash even when the user is missing, so a non-existent address does not
            // answer measurably faster than a wrong password.
            var passwordValid = user is not null
                ? _passwords.Verify(request.Password, user.PasswordHash)
                : _passwords.VerifyDummy(request.Password);

            if (user is null || !passwordValid)
                return Result<AuthTokens>.Fail(ErrorCodes.InvalidCredentials);

            if (!user.IsEmailVerified)
                return Result<AuthTokens>.Fail(ErrorCodes.EmailNotVerified);

            var tokens = await StartNewFamilyAsync(user, context, ct);

            _logger.LogInformation("User {UserId} signed in.", user.Id);
            return Result<AuthTokens>.Ok(tokens);
        }

        // ── REFRESH (rotation + family detonation) ────────────────────────────

        public async Task<Result<AuthTokens>> RefreshAsync(
            string rawRefreshToken, RequestContext context, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenMissing);

            var hash = _secureTokens.Hash(rawRefreshToken);
            var stored = await _refreshTokens.GetByHashAsync(hash, ct);

            if (stored is null)
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenInvalid);

            // ── Replay: this link was already rotated away, so either it leaked or a
            // stale client is retrying. Either way the family can no longer be trusted.
            if (stored.RevokedAt is not null)
            {
                var revoked = await _refreshTokens.RevokeFamilyAsync(
                    stored.FamilyId, RefreshTokenRevokeReason.ReuseDetected, ct);

                _logger.LogWarning(
                    "Refresh token replay for user {UserId}; detonated family {FamilyId} ({Count} tokens revoked).",
                    stored.UserId, stored.FamilyId, revoked);

                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenReused);
            }

            var now = Now;

            if (stored.ExpiresAt <= now)
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenExpired);

            // The family's absolute ceiling — rotation must not be able to push past it.
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

            // Stay inside the family and keep its original ceiling.
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

            return Result<AuthTokens>.Ok(BuildTokens(user, raw, replacement.ExpiresAt));
        }

        // ── LOGOUT ────────────────────────────────────────────────────────────

        public async Task<Result> LogoutAsync(string? rawRefreshToken, CancellationToken ct = default)
        {
            // Signing out is idempotent and never reports failure — the caller has
            // already been told their cookie is gone.
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return Result.Ok();

            var stored = await _refreshTokens.GetByHashAsync(_secureTokens.Hash(rawRefreshToken), ct);

            if (stored is not null)
            {
                // Identity comes from the token itself, not from the access-token claims,
                // so signing out still works after the access token has expired.
                await _refreshTokens.RevokeFamilyAsync(
                    stored.FamilyId, RefreshTokenRevokeReason.Logout, ct);

                _logger.LogInformation("User {UserId} signed out; family {FamilyId} revoked.",
                    stored.UserId, stored.FamilyId);
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

                var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/auth/reset-password?token={raw}";

                await _email.SendAsync(
                    AuthEmailTemplates.PasswordReset(user, link, _settings.PasswordResetMinutes), ct);
            }

            // Same answer whether or not the address exists.
            return Result.Ok();
        }

        public async Task<Result> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken ct = default)
        {
            var policyError = ValidatePassword(newPassword);
            if (policyError is not null) return Result.Fail(policyError);

            var (token, error) = await RedeemTokenAsync(rawToken, UserTokenPurpose.PasswordReset, ct);
            if (error is not null) return Result.Fail(error);

            var user = token!.User;

            user.PasswordHash = _passwords.Hash(newPassword);
            user.PasswordChangedAt = Now;

            // A reset is the remedy for a compromised account, so every existing session
            // has to die — including the attacker's.
            await _users.UpdateAsync(user, ct);
            await _refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokeReason.PasswordChanged, ct);

            token.UsedAt = Now;
            await _userTokens.UpdateAsync(token, ct);

            _logger.LogInformation("Password reset for user {UserId}; all sessions revoked.", user.Id);
            return Result.Ok();
        }

        // ── CHANGE PASSWORD ───────────────────────────────────────────────────

        public async Task<Result<AuthTokens>> ChangePasswordAsync(
            Guid userId, string currentPassword, string newPassword, RequestContext context, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null)
                return Result<AuthTokens>.Fail(ErrorCodes.UserNotFound);

            if (!_passwords.Verify(currentPassword, user.PasswordHash))
                return Result<AuthTokens>.Fail(ErrorCodes.CurrentPasswordIncorrect);

            var policyError = ValidatePassword(newPassword);
            if (policyError is not null)
                return Result<AuthTokens>.Fail(policyError);

            if (_passwords.Verify(newPassword, user.PasswordHash))
                return Result<AuthTokens>.Fail(ErrorCodes.PasswordSameAsCurrent);

            user.PasswordHash = _passwords.Hash(newPassword);
            user.PasswordChangedAt = Now;
            await _users.UpdateAsync(user, ct);

            // Drop every session, then hand the caller a brand-new one so they are not
            // signed out of the device they are currently using.
            await _refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokeReason.PasswordChanged, ct);
            var tokens = await StartNewFamilyAsync(user, context, ct);

            _logger.LogInformation("Password changed for user {UserId}; other sessions revoked.", user.Id);
            return Result<AuthTokens>.Ok(tokens);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string Normalize(string email) => email.Trim().ToLowerInvariant();

        private Error? ValidatePassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password)) return ErrorCodes.PasswordTooWeak;

            return password.Length < _settings.PasswordMinLength
                   || password.Length > _settings.PasswordMaxLength
                ? ErrorCodes.PasswordTooWeak
                : null;
        }

        private async Task<AuthTokens> StartNewFamilyAsync(
            User user, RequestContext context, CancellationToken ct)
        {
            var now = Now;
            var raw = _secureTokens.Generate();

            var token = new RefreshToken
            {
                UserId = user.Id,
                FamilyId = Guid.NewGuid(),               // a sign-in begins a new family
                TokenHash = _secureTokens.Hash(raw),
                ExpiresAt = now.AddDays(_settings.RefreshTokenDays),
                FamilyExpiresAt = now.AddDays(_settings.RefreshFamilyDays),
                CreatedByIp = context.IpAddress,
                UserAgent = context.UserAgent
            };

            await _refreshTokens.AddAsync(token, ct);

            return BuildTokens(user, raw, token.ExpiresAt);
        }

        private AuthTokens BuildTokens(User user, string rawRefreshToken, DateTime refreshExpiresAt) =>
            new(
                AccessToken: _jwt.GenerateToken(user),
                AccessTokenExpiresAt: Now.AddMinutes(_settings.AccessTokenMinutes),
                RefreshToken: rawRefreshToken,
                RefreshTokenExpiresAt: refreshExpiresAt);

        /// <summary>
        /// Loads a one-time token and checks it is redeemable. Distinguishes "already
        /// used" from "expired" from "never existed" so the client can say something
        /// useful.
        /// </summary>
        private async Task<(UserToken? Token, Error? Error)> RedeemTokenAsync(
            string rawToken, string purpose, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                return (null, ErrorCodes.TokenInvalid);

            var token = await _userTokens.GetByHashAsync(_secureTokens.Hash(rawToken), ct);

            if (token is null || token.Purpose != purpose)
                return (null, ErrorCodes.TokenInvalid);

            if (token.UsedAt is not null)
                return (null, ErrorCodes.TokenAlreadyUsed);

            if (token.InvalidatedAt is not null)
                return (null, ErrorCodes.TokenInvalid);

            if (token.ExpiresAt <= Now)
                return (null, ErrorCodes.TokenExpired);

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

            await _email.SendAsync(
                AuthEmailTemplates.VerifyEmail(user, link, _settings.EmailVerificationHours), ct);
        }
    }
}
