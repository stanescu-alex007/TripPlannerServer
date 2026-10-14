using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services.Auth
{
    public class SessionIssuer : ISessionIssuer
    {
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly IJwtService _jwt;
        private readonly ISecureTokenService _secureTokens;
        private readonly IUnitOfWork _unitOfWork;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;

        public SessionIssuer(
            IRefreshTokenRepository refreshTokens,
            IJwtService jwt,
            ISecureTokenService secureTokens,
            IUnitOfWork unitOfWork,
            AuthSettings settings,
            TimeProvider clock)
        {
            _refreshTokens = refreshTokens;
            _jwt = jwt;
            _secureTokens = secureTokens;
            _unitOfWork = unitOfWork;
            _settings = settings;
            _clock = clock;
        }

        public async Task<AuthTokens> StartAsync(
            User user, RequestContext context, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
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

            return Build(user, token, raw, now);
        }

        public async Task<AuthTokens?> TryRotateAsync(
            User user, RefreshToken current, RequestContext context, CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var raw = _secureTokens.Generate();

            var next = now.AddDays(_settings.RefreshTokenDays);

            var replacement = new RefreshToken
            {
                UserId = user.Id,
                FamilyId = current.FamilyId,
                TokenHash = _secureTokens.Hash(raw),

                // Capped, so the cookie is not told to outlive a family that will refuse
                // it anyway once its ceiling passes.
                ExpiresAt = next < current.FamilyExpiresAt ? next : current.FamilyExpiresAt,

                FamilyExpiresAt = current.FamilyExpiresAt,
                CreatedByIp = context.IpAddress,
                UserAgent = context.UserAgent
            };

            // One transaction: the claim on the old link, the new link and the pointer
            // between them land together or not at all.
            var rotated = await _unitOfWork.ExecuteAsync(
                t => _refreshTokens.TryRotateAsync(current, replacement, t), ct);

            return rotated ? Build(user, replacement, raw, now) : null;
        }

        private AuthTokens Build(User user, RefreshToken token, string rawRefreshToken, DateTime now) =>
            new(
                AccessToken: _jwt.GenerateToken(user, token.FamilyId),
                AccessTokenExpiresAt: now.AddMinutes(_settings.AccessTokenMinutes),
                RefreshToken: rawRefreshToken,
                RefreshTokenExpiresAt: token.ExpiresAt);
    }
}
