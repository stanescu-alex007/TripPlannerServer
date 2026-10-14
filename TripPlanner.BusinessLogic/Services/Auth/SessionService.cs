using Microsoft.Extensions.Logging;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services.Auth
{
    public class SessionService : ISessionService
    {
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly IUserRepository _users;
        private readonly ISecureTokenService _secureTokens;
        private readonly ISessionIssuer _sessions;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<SessionService> _logger;

        public SessionService(
            IRefreshTokenRepository refreshTokens,
            IUserRepository users,
            ISecureTokenService secureTokens,
            ISessionIssuer sessions,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<SessionService> logger)
        {
            _refreshTokens = refreshTokens;
            _users = users;
            _secureTokens = secureTokens;
            _sessions = sessions;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

        public async Task<Result<AuthTokens>> RefreshAsync(
            string rawRefreshToken, RequestContext context, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(rawRefreshToken))
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenMissing);

            var stored = await _refreshTokens.GetByHashAsync(_secureTokens.Hash(rawRefreshToken), ct);

            if (stored is null)
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenInvalid);

            var now = _clock.GetUtcNow().UtcDateTime;

            if (stored.RevokedAt is not null)
            {
                // Rotated moments ago: another tab (or a retried request) got there first
                // with the same cookie. Not a theft — continue from the link it got.
                if (IsRecentRotation(stored, now))
                {
                    _logger.LogInformation(
                        "Concurrent refresh for user {UserId} in family {FamilyId}; continuing from the current link.",
                        stored.UserId, stored.FamilyId);

                    return await RotateFamilyHeadAsync(stored.UserId, stored.FamilyId, context, ct);
                }

                // Replay of an already-rotated link outside the grace window, or of one
                // revoked for any other reason: the token leaked, so the entire family can
                // no longer be trusted.
                var revoked = await _refreshTokens.RevokeFamilyAsync(
                    stored.FamilyId, RefreshTokenRevokeReason.ReuseDetected, ct);

                _logger.LogWarning(
                    "Refresh token replay for user {UserId}; detonated family {FamilyId} ({Count} revoked).",
                    stored.UserId, stored.FamilyId, revoked);

                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenReused);
            }

            if (stored.ExpiresAt <= now)
                return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenExpired);

            if (stored.FamilyExpiresAt <= now)
            {
                await _refreshTokens.RevokeFamilyAsync(
                    stored.FamilyId, RefreshTokenRevokeReason.Rotated, ct);
                return Result<AuthTokens>.Fail(ErrorCodes.SessionExpired);
            }

            var user = await _users.GetByIdAsync(stored.UserId, ct);
            var userError = CheckUser(user);
            if (userError is not null) return Result<AuthTokens>.Fail(userError);

            var tokens = await _sessions.TryRotateAsync(user!, stored, context, ct);
            if (tokens is not null) return Result<AuthTokens>.Ok(tokens);

            // Lost the race: a parallel request with the same cookie rotated this link
            // between our read and our write. Same situation as the grace window above,
            // just caught a step later.
            _logger.LogInformation(
                "Refresh for user {UserId} lost a rotation race in family {FamilyId}; continuing from the current link.",
                user!.Id, stored.FamilyId);

            return await RotateFamilyHeadAsync(user, stored.FamilyId, context, ct);
        }

        /// <summary>
        /// Revoked by ordinary rotation (it has a successor) and only seconds ago. Anything
        /// revoked for another reason — logout, password change, a detonated family — or
        /// rotated longer ago still counts as reuse.
        /// </summary>
        private bool IsRecentRotation(RefreshToken token, DateTime now) =>
            token.RevokedReason == RefreshTokenRevokeReason.Rotated
            && token.ReplacedByTokenId is not null
            && token.RevokedAt is not null
            && token.RevokedAt.Value.AddSeconds(_settings.RefreshReuseGraceSeconds) > now;

        private async Task<Result<AuthTokens>> RotateFamilyHeadAsync(
            Guid userId, Guid familyId, RequestContext context, CancellationToken ct)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            var userError = CheckUser(user);
            if (userError is not null) return Result<AuthTokens>.Fail(userError);

            return await RotateFamilyHeadAsync(user!, familyId, context, ct);
        }

        /// <summary>
        /// Issues a fresh pair by rotating the family's current link. The raw value of that
        /// link is unknown here (only its hash is stored), so it cannot simply be handed
        /// back; rotating it keeps exactly one live link per family, which is what reuse
        /// detection relies on.
        /// <para>
        /// Bounded retries, since a third tab can win the race again. Every failure here
        /// is a plain "invalid" and never detonates the family: nothing about it suggests
        /// theft, only a session that was already ended or too much contention.
        /// </para>
        /// </summary>
        private async Task<Result<AuthTokens>> RotateFamilyHeadAsync(
            User user, Guid familyId, RequestContext context, CancellationToken ct)
        {
            const int MaxAttempts = 3;

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var head = await _refreshTokens.GetLiveHeadAsync(familyId, ct);

                // Signed out, revoked or expired since — there is nothing to continue.
                if (head is null || head.UserId != user.Id)
                    return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenInvalid);

                if (head.FamilyExpiresAt <= _clock.GetUtcNow().UtcDateTime)
                    return Result<AuthTokens>.Fail(ErrorCodes.SessionExpired);

                var tokens = await _sessions.TryRotateAsync(user, head, context, ct);
                if (tokens is not null) return Result<AuthTokens>.Ok(tokens);
            }

            _logger.LogWarning(
                "Gave up refreshing family {FamilyId} for user {UserId} after {Attempts} contended attempts.",
                familyId, user.Id, MaxAttempts);

            return Result<AuthTokens>.Fail(ErrorCodes.RefreshTokenInvalid);
        }

        private static Error? CheckUser(User? user)
        {
            if (user is null) return ErrorCodes.RefreshTokenInvalid;
            if (!user.IsEmailVerified) return ErrorCodes.EmailNotVerified;
            return null;
        }

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
    }
}
