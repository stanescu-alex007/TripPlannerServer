using Microsoft.Extensions.Logging;
using TripPlanner.BusinessLogic.Email;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class AccountSecurityService : IAccountSecurityService
    {
        private readonly IUserRepository _users;
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly IPasswordService _passwords;
        private readonly ITwoFactorService _totp;
        private readonly ITwoFactorCredentialService _twoFactorCredentials;
        private readonly IOneTimeTokenService _oneTimeTokens;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _email;

        /// <summary>
        /// Modules holding data keyed by the address. They run inside the email-change
        /// transaction, so the account and everything that refers to it move together.
        /// </summary>
        private readonly IEnumerable<IUserEmailChangedHandler> _emailChangedHandlers;

        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<AccountSecurityService> _logger;

        public AccountSecurityService(
            IUserRepository users,
            IRefreshTokenRepository refreshTokens,
            IPasswordService passwords,
            ITwoFactorService totp,
            ITwoFactorCredentialService twoFactorCredentials,
            IOneTimeTokenService oneTimeTokens,
            IUnitOfWork unitOfWork,
            IEmailSender email,
            IEnumerable<IUserEmailChangedHandler> emailChangedHandlers,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<AccountSecurityService> logger)
        {
            _users = users;
            _refreshTokens = refreshTokens;
            _passwords = passwords;
            _totp = totp;
            _twoFactorCredentials = twoFactorCredentials;
            _oneTimeTokens = oneTimeTokens;
            _unitOfWork = unitOfWork;
            _email = email;
            _emailChangedHandlers = emailChangedHandlers;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

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
            var setup = _totp.GenerateSetup(user.Email);

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
            if (!_totp.VerifyCode(secret, code, out var timeStep))
                return Result<RecoveryCodesResponse>.Fail(ErrorCodes.TwoFactorCodeInvalid);

            var codes = await _twoFactorCredentials.EnableAsync(user, secret, timeStep, ct);

            await _email.TrySendAsync(AuthEmailTemplates.TwoFactorChanged(user, enabled: true), _logger, ct);

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

            await _twoFactorCredentials.DisableAsync(user, ct);

            await _email.TrySendAsync(AuthEmailTemplates.TwoFactorChanged(user, enabled: false), _logger, ct);

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

            var codes = await _twoFactorCredentials.RegenerateRecoveryCodesAsync(user.Id, ct);

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

            var normalized = EmailAddress.Normalize(newEmail);

            if (normalized == user.Email) return Result.Fail(ErrorCodes.EmailUnchanged);

            // Checked here for a clear message, and again at confirm time — the address
            // could be claimed by someone else while the link sits in an inbox.
            if (await _users.ExistsByEmailAsync(normalized, ct))
                return Result.Fail(ErrorCodes.EmailAlreadyRegistered);

            var raw = await _oneTimeTokens.IssueAsync(
                user.Id, UserTokenPurpose.EmailChange,
                TimeSpan.FromHours(_settings.EmailChangeHours), newEmail: normalized, ct: ct);

            // Confirmation goes to the new address, since following the link is what
            // proves ownership of it.
            await _email.TrySendAsync(
                AuthEmailTemplates.EmailChangeRequested(
                    user, normalized, FrontendLinks.ConfirmEmailChange(_settings, raw), _settings.EmailChangeHours),
                _logger, ct);

            // And a warning to the old one — the address an attacker is trying to cut
            // off is exactly the one that needs to hear about it.
            await _email.TrySendAsync(AuthEmailTemplates.EmailChangeNotice(user.Email, normalized), _logger, ct);

            _logger.LogInformation("Email change requested for user {UserId}.", user.Id);
            return Result.Ok();
        }

        public async Task<Result> ConfirmEmailChangeAsync(string rawToken, CancellationToken ct = default)
        {
            var found = await _oneTimeTokens.FindUsableAsync(rawToken, UserTokenPurpose.EmailChange, ct);
            if (!found.Success) return Result.Fail(found.Error!);

            var token = found.Data!;
            if (string.IsNullOrWhiteSpace(token.NewEmail)) return Result.Fail(ErrorCodes.TokenInvalid);

            var newEmail = token.NewEmail;

            // Re-checked at the last moment: someone may have registered that address
            // while this link was waiting in an inbox.
            if (await _users.ExistsByEmailAsync(newEmail, ct))
                return Result.Fail(ErrorCodes.EmailAlreadyRegistered);

            var user = token.User;
            var previousEmail = user.Email;

            bool consumed;

            try
            {
                consumed = await _unitOfWork.ExecuteAsync(async t =>
                {
                    if (!await _oneTimeTokens.TryConsumeAsync(token, t)) return false;

                    user.Email = newEmail;

                    // The new address has just proved itself by receiving this link, so it
                    // counts as verified.
                    user.EmailVerifiedAt = _clock.GetUtcNow().UtcDateTime;
                    await _users.UpdateAsync(user, t);

                    foreach (var handler in _emailChangedHandlers)
                        await handler.HandleAsync(user.Id, previousEmail, newEmail, t);

                    // Sessions are tied to an identity that just changed; forcing a fresh
                    // sign-in keeps a hijacker from riding the old one.
                    await _refreshTokens.RevokeAllForUserAsync(
                        user.Id, RefreshTokenRevokeReason.PasswordChanged, t);

                    return true;
                }, ct);
            }
            catch (DuplicateEmailException)
            {
                // Claimed between the check above and the write; the index decided.
                return Result.Fail(ErrorCodes.EmailAlreadyRegistered);
            }

            if (!consumed) return Result.Fail(ErrorCodes.TokenAlreadyUsed);

            _logger.LogInformation(
                "User {UserId} changed email from {Old} to {New}.", user.Id, previousEmail, newEmail);

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
    }
}
