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

        public async Task AddAsync(Workflow workflow)
            => await _context.Workflows.AddAsync(workflow);

        public void RemoveSteps(IEnumerable<WorkflowStep> steps)
            => _context.WorkflowSteps.RemoveRange(steps);

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}
