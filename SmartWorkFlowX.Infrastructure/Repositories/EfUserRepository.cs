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

        public async Task<List<User>> GetAllWithRolesAsync()
            => await _context.Users.Include(u => u.Role).ToListAsync();

        public async Task<bool> EmailExistsAsync(string email)
            => await _context.Users.AnyAsync(u => u.Email == email);

        public async Task AddAsync(User user)
            => await _context.Users.AddAsync(user);

        public void Remove(User user)
            => _context.Users.Remove(user);

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}
