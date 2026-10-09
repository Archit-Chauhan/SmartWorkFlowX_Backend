using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Infrastructure.Repositories
{
    public class EfWorkflowRepository : IWorkflowRepository
    {
        private readonly SmartWorkflowXDbContext _context;
        public EfWorkflowRepository(SmartWorkflowXDbContext context) => _context = context;

        public async Task<List<Workflow>> GetAllAsync()
            => await _context.Workflows.Include(w => w.Steps).ToListAsync();

        public async Task<(IEnumerable<Workflow> workflows, int total)> GetPaginatedAsync(int page, int pageSize)
        {
            var total = await _context.Workflows.CountAsync();
            var items = await _context.Workflows
                .Include(w => w.Creator)
                .Include(w => w.Steps)
                    .ThenInclude(s => s.ApproverRole)
                .OrderBy(w => w.WorkflowId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<Workflow?> GetByIdWithDetailsAsync(int workflowId)
            => await _context.Workflows
                .Include(w => w.Creator)
                .Include(w => w.Steps)
                    .ThenInclude(s => s.ApproverRole)
                .FirstOrDefaultAsync(w => w.WorkflowId == workflowId);

        public async Task<Workflow?> GetByIdWithStepsAsync(int workflowId)
            => await _context.Workflows
                .Include(w => w.Steps)
                .FirstOrDefaultAsync(w => w.WorkflowId == workflowId);

        public async Task<bool> HasActiveTasksAsync(int workflowId)
            => await _context.Tasks
                .AnyAsync(t => t.WorkflowId == workflowId && t.Status == "In Progress");

        public async Task<bool> TitleExistsAsync(string trimmedTitle, int? excludeWorkflowId)
        {
            var lowered = trimmedTitle.Trim().ToLower();
            return await _context.Workflows.AnyAsync(w =>
                w.Title.Trim().ToLower() == lowered &&
                (excludeWorkflowId == null || w.WorkflowId != excludeWorkflowId));
        }

        public async Task<Dictionary<int, int>> GetActiveTaskCountsAsync(IEnumerable<int> workflowIds)
        {
            var ids = workflowIds.Distinct().ToList();
            if (ids.Count == 0) return new Dictionary<int, int>();

            // SELECT WorkflowId, COUNT(*) FROM Tasks WHERE WorkflowId IN (...) AND Status IN ('Pending','In Progress') GROUP BY WorkflowId
            var rows = await _context.Tasks
                .Where(t => ids.Contains(t.WorkflowId) && (t.Status == "Pending" || t.Status == "In Progress"))
                .GroupBy(t => t.WorkflowId)
                .Select(g => new { WorkflowId = g.Key, Count = g.Count() })
                .ToListAsync();

            return rows.ToDictionary(r => r.WorkflowId, r => r.Count);
        }

        public async Task AddAsync(Workflow workflow)
            => await _context.Workflows.AddAsync(workflow);

        public void RemoveSteps(IEnumerable<WorkflowStep> steps)
            => _context.WorkflowSteps.RemoveRange(steps);

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}
