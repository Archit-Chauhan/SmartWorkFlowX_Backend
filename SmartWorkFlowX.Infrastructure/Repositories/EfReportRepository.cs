using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Infrastructure.Repositories
{
    /// <summary>
    /// Implements analytics queries that require EF Core-specific functions
    /// (DateDiffHour, DateDiffDay, complex GroupBy projections).
    /// Lives in Infrastructure so EF Core can be used freely.
    /// </summary>
    public class EfReportRepository : IReportRepository
    {
        private readonly SmartWorkflowXDbContext _context;
        public EfReportRepository(SmartWorkflowXDbContext context) => _context = context;

        public async Task<SystemAnalyticsDto> GetAnalyticsAsync()
        {
            var now = DateTime.UtcNow;

            var totalUsers = await _context.Users.CountAsync();
            var totalWorkflows = await _context.Workflows.CountAsync();
            var activeWorkflows = await _context.Workflows.CountAsync(w => w.Status == "Active");
            var pendingTasks = await _context.Tasks.CountAsync(t => t.Status == "Pending");
            var inProgressTasks = await _context.Tasks.CountAsync(t => t.Status == "In Progress");
            var completedTasks = await _context.Tasks.CountAsync(t => t.Status == "Completed");

            var overdueTasks = await _context.Tasks.CountAsync(t =>
                t.DueDate.HasValue &&
                t.DueDate < now &&
                t.Status != "Completed" &&
                t.Status != "Cancelled");

            var completedWithTime = await _context.Tasks
                .Where(t => t.CompletedAt.HasValue)
                .Select(t => EF.Functions.DateDiffHour(t.CreatedAt, t.CompletedAt!.Value))
                .ToListAsync();

            var avgCompletionTimeHours = completedWithTime.Any()
                ? completedWithTime.Average()
                : 0.0;

            var userTaskData = await _context.Tasks
                .Where(t => t.AssignedTo != null)
                .GroupBy(t => new { t.AssignedTo, Name = t.Assignee != null ? t.Assignee.Name : "Unknown" })
                .Select(g => new
                {
                    UserName = g.Key.Name,
                    PendingCount = g.Count(t => t.Status == "Pending"),
                    InProgressCount = g.Count(t => t.Status == "In Progress"),
                    CompletedCount = g.Count(t => t.Status == "Completed")
                })
                .ToListAsync();

            var tasksPerUser = userTaskData
                .Select(x => new TasksPerUserDto(x.UserName, x.PendingCount, x.InProgressCount, x.CompletedCount))
                .ToList();

            return new SystemAnalyticsDto(
                totalUsers,
                totalWorkflows,
                activeWorkflows,
                pendingTasks,
                inProgressTasks,
                completedTasks,
                overdueTasks,
                Math.Round(avgCompletionTimeHours, 2),
                tasksPerUser);
        }

        public async Task<List<object>> GetOverdueTasksAsync()
        {
            var now = DateTime.UtcNow;

            return await _context.Tasks
                .Include(t => t.Workflow)
                .Include(t => t.Assignee)
                .Where(t =>
                    t.DueDate.HasValue &&
                    t.DueDate < now &&
                    t.Status != "Completed" &&
                    t.Status != "Cancelled")
                .OrderBy(t => t.DueDate)
                .Select(t => (object)new
                {
                    t.TaskId,
                    t.Title,
                    t.Priority,
                    t.Status,
                    t.DueDate,
                    t.CreatedAt,
                    WorkflowTitle = t.Workflow != null ? t.Workflow.Title : null,
                    AssigneeName = t.Assignee != null ? t.Assignee.Name : "Unassigned",
                    OverdueDays = EF.Functions.DateDiffDay(t.DueDate!.Value, now)
                })
                .ToListAsync();
        }
    }
}


