using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IAuthService
    {
        Task<Result> RegisterAsync(RegisterRequest request);
        Task<AuthResult> LoginAsync(LoginRequest request);
        Task<AuthResult> RefreshAsync(string refreshToken);
        Task<Result> RevokeAsync(Guid userId, string refreshToken);
    }
}
