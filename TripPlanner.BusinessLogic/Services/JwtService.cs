using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services
{
    public class JwtService : IJwtService
    {
        /// <summary>Claim carrying the refresh-token family id.</summary>
        public const string SessionClaim = "sid";

        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;

        public JwtService(AuthSettings settings, TimeProvider clock)
        {
            _settings = settings;
            _clock = clock;
        }

        public string GenerateToken(User user, Guid sessionId)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.JwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = _clock.GetUtcNow().UtcDateTime;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),

                // Unique per token — lets a specific access token be identified in logs.
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

                // Which session this token belongs to — the refresh-token family id.
                new(SessionClaim, sessionId.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _settings.JwtIssuer,
                audience: _settings.JwtAudience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(_settings.AccessTokenMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
