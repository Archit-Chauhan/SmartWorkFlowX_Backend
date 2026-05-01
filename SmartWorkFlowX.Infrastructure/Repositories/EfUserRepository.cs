using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Infrastructure.Repositories
{
    public class EfUserRepository : IUserRepository
    {
        private readonly SmartWorkflowXDbContext _context;
        public EfUserRepository(SmartWorkflowXDbContext context) => _context = context;

        public async Task<User?> GetByIdAsync(int userId)
            => await _context.Users.FindAsync(userId);

        public async Task<User?> GetByEmailWithRoleAsync(string email)
            => await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

        public async Task<IEnumerable<User>> GetAllWithRolesAsync()
            => await _context.Users.Include(u => u.Role).ToListAsync();

        public async Task<(IEnumerable<User> users, int total)> GetPaginatedAsync(int page, int pageSize)
        {
            var query = _context.Users.Include(u => u.Role);
            var total = await query.CountAsync();
            var items = await query
                .OrderBy(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
                
            return (items, total);
        }

        public async Task<bool> EmailExistsAsync(string email)
            => await _context.Users.AnyAsync(u => u.Email == email);

        public async Task AddAsync(User user)
            => await _context.Users.AddAsync(user);

        public Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;
            }
        }

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}
