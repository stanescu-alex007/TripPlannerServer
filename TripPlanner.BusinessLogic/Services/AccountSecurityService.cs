using Microsoft.Extensions.Logging;
using TripPlanner.BusinessLogic.Email;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class AccountSecurityService : IAccountSecurityService
    {
        private readonly IUserRepository _users;
        private readonly IUserTokenRepository _userTokens;
        private readonly IUserRecoveryCodeRepository _recoveryCodes;
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly IPasswordService _passwords;
        private readonly ISecureTokenService _secureTokens;
        private readonly ITwoFactorService _twoFactor;
        private readonly IEmailSender _email;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<AccountSecurityService> _logger;

        public AccountSecurityService(
            IUserRepository users,
            IUserTokenRepository userTokens,
            IUserRecoveryCodeRepository recoveryCodes,
            IRefreshTokenRepository refreshTokens,
            IPasswordService passwords,
            ISecureTokenService secureTokens,
            ITwoFactorService twoFactor,
            IEmailSender email,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<AccountSecurityService> logger)
        {
            _users = users;
            _userTokens = userTokens;
            _recoveryCodes = recoveryCodes;
            _refreshTokens = refreshTokens;
            _passwords = passwords;
            _secureTokens = secureTokens;
            _twoFactor = twoFactor;
            _email = email;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // ── TWO-FACTOR ────────────────────────────────────────────────────────

        public async Task<Result<TwoFactorSetupResponse>> BeginTwoFactorSetupAsync(
            Guid userId, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result<TwoFactorSetupResponse>.Fail(ErrorCodes.UserNotFound);

            if (user.IsTwoFactorEnabled)
                return Result<TwoFactorSetupResponse>.Fail(ErrorCodes.TwoFactorAlreadyEnabled);

            // The secret is handed out but not stored: if the user walks away now, the
            // account is untouched and they simply start over.
            var setup = _twoFactor.GenerateSetup(user.Email);

            return Result<TwoFactorSetupResponse>.Ok(
                new TwoFactorSetupResponse(setup.Secret, setup.OtpAuthUri));
        }

        public async Task<Result<RecoveryCodesResponse>> EnableTwoFactorAsync(
            Guid userId, string secret, string code, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result<RecoveryCodesResponse>.Fail(ErrorCodes.UserNotFound);

            if (user.IsTwoFactorEnabled)
                return Result<RecoveryCodesResponse>.Fail(ErrorCodes.TwoFactorAlreadyEnabled);

            if (string.IsNullOrWhiteSpace(secret))
                return Result<RecoveryCodesResponse>.Fail(ErrorCodes.TwoFactorSetupMissing);

            // Proving one live code confirms the secret really is in their app —
            // without this, enabling 2FA could lock the user out permanently.
            if (!_twoFactor.VerifyCode(secret, code))
                return Result<RecoveryCodesResponse>.Fail(ErrorCodes.TwoFactorCodeInvalid);

            user.TwoFactorSecret = secret;
            user.TwoFactorEnabledAt = Now;
            await _users.UpdateAsync(user, ct);

            var codes = await IssueRecoveryCodesAsync(user, ct);

            await TrySendAsync(AuthEmailTemplates.TwoFactorChanged(user, enabled: true), ct);

            _logger.LogInformation("Two-factor enabled for user {UserId}.", user.Id);
            return Result<RecoveryCodesResponse>.Ok(new RecoveryCodesResponse(codes));
        }

        public async Task<Result> DisableTwoFactorAsync(
            Guid userId, string password, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result.Fail(ErrorCodes.UserNotFound);

            if (!user.IsTwoFactorEnabled) return Result.Fail(ErrorCodes.TwoFactorNotEnabled);

            var passwordError = VerifyPassword(user, password);
            if (passwordError is not null) return Result.Fail(passwordError);

            user.TwoFactorSecret = null;
            user.TwoFactorEnabledAt = null;
            await _users.UpdateAsync(user, ct);

            // Recovery codes only exist to get past 2FA; without it they are a second
            // set of credentials nobody is watching.
            await _recoveryCodes.DeleteAllAsync(user.Id, ct);

            await TrySendAsync(AuthEmailTemplates.TwoFactorChanged(user, enabled: false), ct);

            _logger.LogWarning("Two-factor disabled for user {UserId}.", user.Id);
            return Result.Ok();
        }

        public async Task<Result<RecoveryCodesResponse>> RegenerateRecoveryCodesAsync(
            Guid userId, string password, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result<RecoveryCodesResponse>.Fail(ErrorCodes.UserNotFound);

            if (!user.IsTwoFactorEnabled)
                return Result<RecoveryCodesResponse>.Fail(ErrorCodes.TwoFactorNotEnabled);

            var passwordError = VerifyPassword(user, password);
            if (passwordError is not null) return Result<RecoveryCodesResponse>.Fail(passwordError);

            var codes = await IssueRecoveryCodesAsync(user, ct);

            _logger.LogInformation("Recovery codes regenerated for user {UserId}.", user.Id);
            return Result<RecoveryCodesResponse>.Ok(new RecoveryCodesResponse(codes));
        }

        // ── EMAIL CHANGE ──────────────────────────────────────────────────────

        public async Task<Result> RequestEmailChangeAsync(
            Guid userId, string newEmail, string password, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result.Fail(ErrorCodes.UserNotFound);

            var passwordError = VerifyPassword(user, password);
            if (passwordError is not null) return Result.Fail(passwordError);

            var normalized = newEmail.Trim().ToLowerInvariant();

            if (normalized == user.Email) return Result.Fail(ErrorCodes.EmailUnchanged);

            // Checked here for a clear message, and again at confirm time — the address
            // could be claimed by someone else while the link sits in an inbox.
            if (await _users.ExistsByEmailAsync(normalized, ct))
                return Result.Fail(ErrorCodes.EmailAlreadyRegistered);

            await _userTokens.InvalidateOutstandingAsync(user.Id, UserTokenPurpose.EmailChange, ct);

            var raw = _secureTokens.Generate();

            await _userTokens.AddAsync(new UserToken
            {
                UserId = user.Id,
                TokenHash = _secureTokens.Hash(raw),
                Purpose = UserTokenPurpose.EmailChange,
                NewEmail = normalized,
                ExpiresAt = Now.AddHours(_settings.EmailChangeHours)
            }, ct);

            var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/auth/confirm-email-change?token={raw}";

            // Confirmation goes to the new address, since following the link is what
            // proves ownership of it.
            await TrySendAsync(
                AuthEmailTemplates.EmailChangeRequested(user, normalized, link, _settings.EmailChangeHours), ct);

            // And a warning to the old one — the address an attacker is trying to cut
            // off is exactly the one that needs to hear about it.
            await TrySendAsync(AuthEmailTemplates.EmailChangeNotice(user.Email, normalized), ct);

            _logger.LogInformation("Email change requested for user {UserId}.", user.Id);
            return Result.Ok();
        }

        public async Task<Result> ConfirmEmailChangeAsync(string rawToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawToken)) return Result.Fail(ErrorCodes.TokenInvalid);

            var token = await _userTokens.GetByHashAsync(_secureTokens.Hash(rawToken), ct);

            if (token is null || token.Purpose != UserTokenPurpose.EmailChange)
                return Result.Fail(ErrorCodes.TokenInvalid);

            if (token.UsedAt is not null) return Result.Fail(ErrorCodes.TokenAlreadyUsed);
            if (token.InvalidatedAt is not null) return Result.Fail(ErrorCodes.TokenInvalid);
            if (token.ExpiresAt <= Now) return Result.Fail(ErrorCodes.TokenExpired);
            if (string.IsNullOrWhiteSpace(token.NewEmail)) return Result.Fail(ErrorCodes.TokenInvalid);

            // Re-checked at the last moment: someone may have registered that address
            // while this link was waiting in an inbox.
            if (await _users.ExistsByEmailAsync(token.NewEmail, ct))
                return Result.Fail(ErrorCodes.EmailAlreadyRegistered);

            var user = token.User;
            var previousEmail = user.Email;

            user.Email = token.NewEmail;

            // The new address has just proved itself by receiving this link, so it
            // counts as verified.
            user.EmailVerifiedAt = Now;
            await _users.UpdateAsync(user, ct);

            token.UsedAt = Now;
            await _userTokens.UpdateAsync(token, ct);

            // Sessions are tied to an identity that just changed; forcing a fresh
            // sign-in keeps a hijacker from riding the old one.
            await _refreshTokens.RevokeAllForUserAsync(user.Id, RefreshTokenRevokeReason.PasswordChanged, ct);

            _logger.LogInformation(
                "User {UserId} changed email from {Old} to {New}.", user.Id, previousEmail, user.Email);

            return Result.Ok();
        }

        // ── SESSIONS ──────────────────────────────────────────────────────────

        public async Task<Result<List<SessionDto>>> GetSessionsAsync(
            Guid userId, Guid? currentSessionId, CancellationToken ct = default)
        {
            var sessions = await _refreshTokens.GetActiveSessionsAsync(userId, ct);

            return Result<List<SessionDto>>.Ok(sessions.Select(s => new SessionDto
            {
                Id = s.FamilyId,
                IpAddress = s.CreatedByIp,
                UserAgent = s.UserAgent,
                DeviceLabel = DescribeDevice(s.UserAgent),
                CreatedAt = s.CreatedAt,
                ExpiresAt = s.ExpiresAt,
                IsCurrent = s.FamilyId == currentSessionId
            }).ToList());
        }

        public async Task<Result> RevokeSessionAsync(
            Guid userId, Guid sessionId, CancellationToken ct = default)
        {
            var revoked = await _refreshTokens.RevokeFamilyForUserAsync(
                userId, sessionId, RefreshTokenRevokeReason.Logout, ct);

            // Scoped to the user, so a guessed id cannot end someone else's session.
            return revoked ? Result.Ok() : Result.Fail(ErrorCodes.NotFoundSession);
        }

        public async Task<Result<int>> RevokeOtherSessionsAsync(
            Guid userId, Guid? currentSessionId, CancellationToken ct = default)
        {
            // Guid.Empty matches no family, so a caller we cannot place ends every
            // session including their own — the safe reading of "sign out everywhere
            // else" when "here" is unknown.
            var count = await _refreshTokens.RevokeAllExceptFamilyAsync(
                userId, currentSessionId ?? Guid.Empty, RefreshTokenRevokeReason.Logout, ct);

            _logger.LogInformation("User {UserId} revoked {Count} other sessions.", userId, count);
            return Result<int>.Ok(count);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Error? VerifyPassword(User user, string password)
        {
            if (user.PasswordHash is null) return ErrorCodes.PasswordNotSet;

            return _passwords.Verify(password, user.PasswordHash)
                ? null
                : ErrorCodes.CurrentPasswordIncorrect;
        }

        private async Task<IReadOnlyList<string>> IssueRecoveryCodesAsync(User user, CancellationToken ct)
        {
            var plain = _twoFactor.GenerateRecoveryCodes(_settings.RecoveryCodeCount);

            // Hashed like passwords: the table must not be a way around 2FA.
            var entities = plain.Select(code => new UserRecoveryCode
            {
                UserId = user.Id,
                CodeHash = _passwords.Hash(code)
            });

            await _recoveryCodes.ReplaceAllAsync(user.Id, entities, ct);

            return plain;
        }

        /// <summary>
        /// A rough, readable device name. Full user-agent parsing is not worth a
        /// dependency here — this only has to help someone recognise their own device.
        /// </summary>
        private static string DescribeDevice(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown device";

            var ua = userAgent;

            var browser =
                ua.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge" :
                ua.Contains("OPR/", StringComparison.OrdinalIgnoreCase) ? "Opera" :
                ua.Contains("Firefox", StringComparison.OrdinalIgnoreCase) ? "Firefox" :
                ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ? "Chrome" :
                ua.Contains("Safari", StringComparison.OrdinalIgnoreCase) ? "Safari" :
                "Browser";

            var platform =
                ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android" :
                ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ? "iPhone" :
                ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iPad" :
                ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows" :
                ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) ? "macOS" :
                ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux" :
                null;

            return platform is null ? browser : $"{browser} on {platform}";
        }

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
