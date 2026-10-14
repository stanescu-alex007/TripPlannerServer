using Microsoft.Extensions.Logging;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services.Auth
{
    public class TwoFactorCredentialService : ITwoFactorCredentialService
    {
        private readonly IUserRepository _users;
        private readonly IUserRecoveryCodeRepository _recoveryCodes;
        private readonly ITwoFactorService _totp;
        private readonly ISecretProtector _protector;
        private readonly IPasswordService _passwords;
        private readonly IUnitOfWork _unitOfWork;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<TwoFactorCredentialService> _logger;

        public TwoFactorCredentialService(
            IUserRepository users,
            IUserRecoveryCodeRepository recoveryCodes,
            ITwoFactorService totp,
            ISecretProtector protector,
            IPasswordService passwords,
            IUnitOfWork unitOfWork,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<TwoFactorCredentialService> logger)
        {
            _users = users;
            _recoveryCodes = recoveryCodes;
            _totp = totp;
            _protector = protector;
            _passwords = passwords;
            _unitOfWork = unitOfWork;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

        /// <summary>
        /// Binds each ciphertext to its account: a secret copied into another user's row
        /// fails to decrypt instead of letting one authenticator open both.
        /// </summary>
        private static string PurposeFor(Guid userId) => $"two-factor-secret:{userId:N}";

        public Task<IReadOnlyList<string>> EnableAsync(
            User user, string secret, long confirmedTimeStep, CancellationToken ct = default) =>
            // One transaction: 2FA switched on without its recovery codes would leave a
            // user who loses their phone with no way back in.
            _unitOfWork.ExecuteAsync(async token =>
            {
                user.TwoFactorSecret = _protector.Protect(secret, PurposeFor(user.Id));
                user.TwoFactorEnabledAt = _clock.GetUtcNow().UtcDateTime;

                // The code that switched 2FA on is spent, like any other.
                user.TwoFactorLastStep = confirmedTimeStep;

                await _users.UpdateAsync(user, token);

                return await IssueRecoveryCodesAsync(user.Id, token);
            }, ct);

        public Task DisableAsync(User user, CancellationToken ct = default) =>
            _unitOfWork.ExecuteAsync(async token =>
            {
                user.TwoFactorSecret = null;
                user.TwoFactorEnabledAt = null;
                await _users.UpdateAsync(user, token);

                // Recovery codes only exist to get past 2FA; without it they are a second
                // set of credentials nobody is watching.
                await _recoveryCodes.DeleteAllAsync(user.Id, token);
            }, ct);

        public Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(
            Guid userId, CancellationToken ct = default) =>
            IssueRecoveryCodesAsync(userId, ct);

        public async Task<bool> VerifyAsync(User user, string code, CancellationToken ct = default)
        {
            if (!user.IsTwoFactorEnabled || string.IsNullOrWhiteSpace(code)) return false;

            var normalized = code.Trim().Replace(" ", string.Empty);

            // An authenticator code is six digits and a recovery code never is, so the
            // shape decides which check applies. Without this, every mistyped TOTP code
            // also paid for an Argon2 comparison against each remaining recovery code.
            if (normalized.Length == 6 && normalized.All(char.IsAsciiDigit))
            {
                var secret = ReadSecret(user);

                if (secret is null || !_totp.VerifyCode(secret, normalized, out var timeStep))
                    return false;

                // The code is right, but a code is only good once: the same six digits
                // stay valid for up to a minute and a half, and whoever saw them in that
                // time must not be able to reuse them.
                if (await _users.TryConsumeTwoFactorStepAsync(user, timeStep, ct))
                    return true;

                _logger.LogWarning(
                    "Two-factor code replayed for user {UserId}; it had already been used.", user.Id);

                return false;
            }

            return await TryConsumeRecoveryCodeAsync(user.Id, normalized.ToLowerInvariant(), ct);
        }

        public async Task<int> ProtectLegacySecretsAsync(CancellationToken ct = default)
        {
            var legacy = await _users.GetWithUnprotectedTwoFactorSecretAsync(_protector.Marker, ct);
            var converted = 0;

            foreach (var user in legacy)
            {
                var plaintext = user.TwoFactorSecret!;

                var replaced = await _users.TryReplaceTwoFactorSecretAsync(
                    user.Id, plaintext, _protector.Protect(plaintext, PurposeFor(user.Id)), ct);

                if (replaced) converted++;
            }

            return converted;
        }

        private string? ReadSecret(User user)
        {
            if (user.TwoFactorSecret is null) return null;

            var secret = _protector.Unprotect(user.TwoFactorSecret, PurposeFor(user.Id));

            if (secret is null)
            {
                // Either the key that wrote it is no longer configured or the value was
                // altered. Recovery codes are now the only way into this account.
                _logger.LogError(
                    "Two-factor secret for user {UserId} could not be decrypted; only recovery codes will work.",
                    user.Id);
            }

            return secret;
        }

        private async Task<bool> TryConsumeRecoveryCodeAsync(Guid userId, string code, CancellationToken ct)
        {
            if (code.Length < 4) return false;

            // Hashes are salted per code, so each candidate has to be checked in turn
            // rather than looked up.
            foreach (var candidate in await _recoveryCodes.GetUnusedAsync(userId, ct))
            {
                if (!_passwords.Verify(code, candidate.CodeHash)) continue;

                if (!await _recoveryCodes.TryMarkUsedAsync(candidate.Id, ct)) return false;

                var left = await _recoveryCodes.CountUnusedAsync(userId, ct);
                _logger.LogWarning(
                    "User {UserId} signed in with a recovery code; {Remaining} left.", userId, left);

                return true;
            }

            return false;
        }

        private async Task<IReadOnlyList<string>> IssueRecoveryCodesAsync(Guid userId, CancellationToken ct)
        {
            var plain = _totp.GenerateRecoveryCodes(_settings.RecoveryCodeCount);

            // Hashed like passwords: the table must not be a way around 2FA.
            var entities = plain.Select(code => new UserRecoveryCode
            {
                UserId = userId,
                CodeHash = _passwords.Hash(code)
            });

            await _recoveryCodes.ReplaceAllAsync(userId, entities, ct);

            return plain;
        }
    }
}
