using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IRepositories
{
    public interface IRefreshTokenRepository
    {
        Task CreateAsync(Guid userId, string token, string ip);
        Task<RefreshToken?> GetAsync(string token);
        Task UpdateAsync(RefreshToken token);
    }
}
