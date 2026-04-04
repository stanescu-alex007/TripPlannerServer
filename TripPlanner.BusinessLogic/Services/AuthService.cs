using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshRepository;
        private readonly IPasswordService _passwordService;
        private readonly IJwtService _jwtService;

        public AuthService(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshRepository,
            IPasswordService passwordService,
            IJwtService jwtService)
        {
            _userRepository = userRepository;
            _refreshRepository = refreshRepository;
            _passwordService = passwordService;
            _jwtService = jwtService;
        }

        // 🟢 REGISTER
        public async Task<Result> RegisterAsync(RegisterRequest request)
        {
            var email = request.Email.ToLower().Trim();

            var exists = await _userRepository.ExistsByEmailAsync(email);

            if (exists)
                return Result.Fail("Email already exists");

            var hash = _passwordService.Hash(request.Password);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                PasswordHash = hash
            };

            await _userRepository.AddAsync(user);

            return Result.Ok();
        }

        // 🟢 LOGIN
        public async Task<AuthResult> LoginAsync(LoginRequest request)
        {
            var email = request.Email.ToLower().Trim();

            var user = await _userRepository.GetByEmailAsync(email);

            if (user == null)
                return AuthResult.Fail("Invalid credentials");

            var valid = _passwordService.Verify(request.Password, user.PasswordHash);

            if (!valid)
                return AuthResult.Fail("Invalid credentials");

            var accessToken = _jwtService.GenerateToken(user);
            var refreshToken = _jwtService.GenerateRefreshToken();

            // 🔥 IMPORTANT: token plain + IP (optional)
            await _refreshRepository.CreateAsync(user.Id, refreshToken, "unknown");

            return new AuthResult(accessToken, refreshToken);
        }

        // 🔄 REFRESH TOKEN (ROTATION)
        public async Task<AuthResult> RefreshAsync(string token)
        {
            var stored = await _refreshRepository.GetAsync(token);

            if (stored == null ||
                stored.RevokedAt != null ||
                stored.ExpiresAt < DateTime.UtcNow)
            {
                return AuthResult.Fail("Invalid refresh token");
            }

            // 🔥 ROTATE: revoke old token
            stored.RevokedAt = DateTime.UtcNow;

            var newRefreshToken = _jwtService.GenerateRefreshToken();

            var newRefreshTokenHash = _passwordService.Hash(newRefreshToken);

            // 🔁 legăm token-urile
            stored.ReplacedByToken = newRefreshTokenHash;

            await _refreshRepository.UpdateAsync(stored);

            var user = await _userRepository.GetByIdAsync(stored.UserId);

            if (user == null)
                return AuthResult.Fail("User not found");

            var newAccess = _jwtService.GenerateToken(user);

            // 🔥 salvăm noul refresh (hash-uit în repo)
            await _refreshRepository.CreateAsync(user.Id, newRefreshToken, "unknown");

            return new AuthResult(newAccess, newRefreshToken);
        }

        public async Task<Result> RevokeAsync(Guid userId, string token)
        {
            var stored = await _refreshRepository.GetAsync(token);

            if (stored == null || stored.UserId != userId)
                return Result.Fail("Invalid token");

            if (stored.RevokedAt != null)
                return Result.Fail("Already revoked");

            stored.RevokedAt = DateTime.UtcNow;
            stored.RevokedByIp = "unknown"; // 🔥 o să îmbunătățim după

            await _refreshRepository.UpdateAsync(stored);

            return Result.Ok();
        }
    }
}