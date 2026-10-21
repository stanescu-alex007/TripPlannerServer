using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services.Auth
{
    public class OneTimeTokenService : IOneTimeTokenService
    {
        private readonly IUserTokenRepository _tokens;
        private readonly ISecureTokenService _secureTokens;
        private readonly TimeProvider _clock;

        public OneTimeTokenService(
            IUserTokenRepository tokens,
            ISecureTokenService secureTokens,
            TimeProvider clock)
        {
            _tokens = tokens;
            _secureTokens = secureTokens;
            _clock = clock;
        }

        public async Task<string> IssueAsync(
            Guid userId, string purpose, TimeSpan lifetime,
            string? newEmail = null, CancellationToken ct = default)
        {
            await _tokens.InvalidateOutstandingAsync(userId, purpose, ct);

            var raw = _secureTokens.Generate();

            await _tokens.AddAsync(new UserToken
            {
                UserId = userId,
                TokenHash = _secureTokens.Hash(raw),
                Purpose = purpose,
                NewEmail = newEmail,
                ExpiresAt = _clock.GetUtcNow().UtcDateTime.Add(lifetime)
            }, ct);

            return raw;
        }

        public Task<bool> IssuedRecentlyAsync(
            Guid userId, string purpose, TimeSpan window, CancellationToken ct = default) =>
            _tokens.IssuedSinceAsync(userId, purpose, _clock.GetUtcNow().UtcDateTime - window, ct);

        public async Task<Result<UserToken>> FindUsableAsync(
            string rawToken, string purpose, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                return Result<UserToken>.Fail(ErrorCodes.TokenInvalid);

            var token = await _tokens.GetByHashAsync(_secureTokens.Hash(rawToken), ct);

            // A token presented for the wrong flow is treated as unknown, not as a
            // mismatch — nothing about other purposes is revealed.
            if (token is null || token.Purpose != purpose)
                return Result<UserToken>.Fail(ErrorCodes.TokenInvalid);

            if (token.UsedAt is not null) return Result<UserToken>.Fail(ErrorCodes.TokenAlreadyUsed);
            if (token.InvalidatedAt is not null) return Result<UserToken>.Fail(ErrorCodes.TokenInvalid);
            if (token.ExpiresAt <= _clock.GetUtcNow().UtcDateTime) return Result<UserToken>.Fail(ErrorCodes.TokenExpired);

            return Result<UserToken>.Ok(token);
        }

        public Task<bool> TryConsumeAsync(UserToken token, CancellationToken ct = default) =>
            _tokens.TryMarkUsedAsync(token.Id, ct);
    }
}
