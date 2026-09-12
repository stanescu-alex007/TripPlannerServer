using Microsoft.EntityFrameworkCore;
using Npgsql;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;

namespace TripPlanner.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        /// <summary>Postgres SQLSTATE for unique_violation.</summary>
        private const string UniqueViolation = "23505";

        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context) => _context = context;

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
            _context.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
            _context.Users.AnyAsync(u => u.Email == email, ct);

        public async Task AddAsync(User user, CancellationToken ct = default)
        {
            await _context.Users.AddAsync(user, ct);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                               { SqlState: UniqueViolation } pg
                                               && pg.ConstraintName?.Contains("Email") == true)
            {
                // Lost a race against a concurrent registration for the same address.
                _context.Entry(user).State = EntityState.Detached;
                throw new DuplicateEmailException(user.Email, ex);
            }
        }

        public async Task UpdateAsync(User user, CancellationToken ct = default)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(User user, CancellationToken ct = default)
        {
            // The audit interceptor turns this into a DeletedAt stamp.
            _context.Users.Remove(user);
            await _context.SaveChangesAsync(ct);
        }
    }
}
