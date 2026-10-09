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

        public async Task<List<DashboardTaskRow>> GetDashboardTaskRowsAsync(DateTime previousStart, DateTime end, int? scopedUserId)
        {
            // Only tasks that can change a number: created in [previousStart, end], or older but completed since
            // previousStart, or older and still open (Cancelled/Rejected tasks have no completion date and never count as open).
            var query = _context.Tasks
                .AsNoTracking()
                .Where(t => t.CreatedAt <= end &&
                    (t.CreatedAt >= previousStart ||
                     (t.CompletedAt != null && t.CompletedAt >= previousStart) ||
                     (t.CompletedAt == null && t.Status != "Cancelled" && t.Status != "Rejected")));

            if (scopedUserId.HasValue)
            {
                int uid = scopedUserId.Value;
                query = query.Where(t =>
                    t.AssignedTo == uid ||
                    t.OriginalAssignedTo == uid ||
                    t.StepHistories.Any(h => h.ActedByUserId == uid));
            }

            return await query
                .Select(t => new DashboardTaskRow
                {
                    TaskId = t.TaskId,
                    Title = t.Title,
                    WorkflowId = t.WorkflowId,
                    WorkflowTitle = t.Workflow != null ? t.Workflow.Title : null,
                    AssignedTo = t.AssignedTo,
                    OriginalAssignedTo = t.OriginalAssignedTo,
                    Status = t.Status,
                    Priority = t.Priority,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category != null ? t.Category.Name : null,
                    CategoryColor = t.Category != null ? t.Category.ColorHex : null,
                    CreatedAt = t.CreatedAt,
                    DueDate = t.DueDate,
                    CompletedAt = t.CompletedAt
                })
                .ToListAsync();
        }

        public async Task<DashboardLookups> GetDashboardLookupsAsync()
        {
            // Deleted users are included so historical tasks still show the right name; callers hide them from dropdowns and totals.
            var users = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(u => new DashboardUserRow { Id = u.UserId, Name = u.Name, IsDeleted = u.IsDeleted })
                .ToListAsync();

            var workflows = await _context.Workflows
                .AsNoTracking()
                .Select(w => new DashboardWorkflowRow { Id = w.WorkflowId, Title = w.Title, Status = w.Status })
                .ToListAsync();

            var categories = await _context.TaskCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new DashboardCategoryRow { Id = c.CategoryId, Name = c.Name, ColorHex = c.ColorHex })
                .ToListAsync();

            return new DashboardLookups { Users = users, Workflows = workflows, Categories = categories };
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


