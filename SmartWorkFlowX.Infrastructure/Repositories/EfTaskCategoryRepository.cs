using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Infrastructure.Repositories
{
    public class EfTaskCategoryRepository : ITaskCategoryRepository
    {
        private readonly SmartWorkflowXDbContext _context;

        public EfTaskCategoryRepository(SmartWorkflowXDbContext context)
        {
            _context = context;
        }

        public async Task<List<TaskCategory>> GetAllActiveAsync()
            => await _context.TaskCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}
