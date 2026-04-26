using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{

    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly AppDbContext _context;

        public RefreshTokenRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Guid userId, string token, string ip)
        {
            var refresh = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = Hash(token),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedByIp = ip
            };

            await _context.RefreshTokens.AddAsync(refresh);
            await _context.SaveChangesAsync();
        }

        public async Task<RefreshToken?> GetAsync(string token)
        {
            var hash = Hash(token);

            return await _context.RefreshTokens
                .FirstOrDefaultAsync(x => x.TokenHash == hash);
        }

        public async Task UpdateAsync(RefreshToken token)
        {
            _context.RefreshTokens.Update(token);
            await _context.SaveChangesAsync();
        }

        private string Hash(string token)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }
    }
}
