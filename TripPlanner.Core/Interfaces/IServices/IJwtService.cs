using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        string GenerateRefreshToken();
    }
}
