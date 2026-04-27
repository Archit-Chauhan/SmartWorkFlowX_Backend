using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Infrastructure.Repositories
{
    public class EfRoleRepository : IRoleRepository
    {
        private readonly SmartWorkflowXDbContext _context;
        public EfRoleRepository(SmartWorkflowXDbContext context) => _context = context;

        public async Task<List<Role>> GetAllAsync()
            => await _context.Roles.ToListAsync();
    }
}
