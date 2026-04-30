using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Infrastructure.Repositories
{
    public class EfTaskRepository : ITaskRepository
    {
        private readonly SmartWorkflowXDbContext _context;
        public EfTaskRepository(SmartWorkflowXDbContext context) => _context = context;

        public async Task<TaskItem?> GetByIdWithWorkflowAsync(int taskId)
            => await _context.Tasks
                .Include(t => t.Workflow)
                    .ThenInclude(w => w!.Steps)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);

        public async Task<List<TaskItem>> GetMyTasksAsync(int userId)
            => await _context.Tasks
                .Include(t => t.Workflow)
                .Where(t => t.AssignedTo == userId && t.Status != "Completed" && t.Status != "Cancelled")
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

        public async Task<List<TaskItem>> GetAllFilteredAsync(string? status, string? priority, int? assignedTo)
        {
            var query = _context.Tasks
                .Include(t => t.Workflow)
                .Include(t => t.Assignee)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.Status == status);

            if (!string.IsNullOrEmpty(priority))
                query = query.Where(t => t.Priority == priority);

            if (assignedTo.HasValue)
                query = query.Where(t => t.AssignedTo == assignedTo);

            return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        }

        public async Task<List<TaskStepHistory>> GetHistoryAsync(int taskId)
            => await _context.TaskStepHistories
                .Include(h => h.ActedByUser)
                .Where(h => h.TaskId == taskId)
                .OrderBy(h => h.StepOrder)
                .ThenBy(h => h.ActedAt)
                .ToListAsync();

        public async Task<User?> GetFirstUserByRoleAsync(int roleId)
            => await _context.Users.FirstOrDefaultAsync(u => u.RoleId == roleId);

        public async Task<List<TaskItem>> GetMyActivityAsync(int userId)
        {
            // Get IDs of tasks the user has acted on
            var actedTaskIds = await _context.TaskStepHistories
                .Where(h => h.ActedByUserId == userId)
                .Select(h => h.TaskId)
                .Distinct()
                .ToListAsync();

            // Return those tasks, excluding ones currently assigned to the user (those show in Action Center)
            return await _context.Tasks
                .Include(t => t.Workflow)
                .Where(t => actedTaskIds.Contains(t.TaskId) && t.AssignedTo != userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<(IEnumerable<TaskItem> tasks, int total)> GetMyTasksPaginatedAsync(int userId, int page, int pageSize)
        {
            var query = _context.Tasks
                .Include(t => t.Workflow)
                .Where(t => t.AssignedTo == userId && t.Status != "Completed" && t.Status != "Cancelled");

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<(IEnumerable<TaskItem> tasks, int total)> GetMyActivityPaginatedAsync(int userId, int page, int pageSize)
        {
            var actedTaskIds = await _context.TaskStepHistories
                .Where(h => h.ActedByUserId == userId)
                .Select(h => h.TaskId)
                .Distinct()
                .ToListAsync();

            var query = _context.Tasks
                .Include(t => t.Workflow)
                .Where(t => actedTaskIds.Contains(t.TaskId) && t.AssignedTo != userId);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task AddAsync(TaskItem task)
            => await _context.Tasks.AddAsync(task);

        public async Task AddHistoryAsync(TaskStepHistory history)
            => await _context.TaskStepHistories.AddAsync(history);

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}
