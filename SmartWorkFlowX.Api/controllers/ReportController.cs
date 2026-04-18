using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Auditor")]
    public class ReportController : ControllerBase
    {
        private readonly SmartWorkflowXDbContext _context;

        public ReportController(SmartWorkflowXDbContext context)
        {
            _context = context;
        }

        // GET: api/Report/analytics — Rich dashboard analytics
        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
        {
            var now = DateTime.UtcNow;

            // Core counts
            var totalUsers = await _context.Users.CountAsync();
            var totalWorkflows = await _context.Workflows.CountAsync();
            var activeWorkflows = await _context.Workflows.CountAsync(w => w.Status == "Active");
            var pendingTasks = await _context.Tasks.CountAsync(t => t.Status == "Pending");
            var inProgressTasks = await _context.Tasks.CountAsync(t => t.Status == "In Progress");
            var completedTasks = await _context.Tasks.CountAsync(t => t.Status == "Completed");

            // Overdue tasks: past DueDate and not yet completed/cancelled
            var overdueTasks = await _context.Tasks.CountAsync(t =>
                t.DueDate.HasValue &&
                t.DueDate < now &&
                t.Status != "Completed" &&
                t.Status != "Cancelled");

            // Average completion time (hours) for tasks that have CompletedAt set
            var completedWithTime = await _context.Tasks
                .Where(t => t.CompletedAt.HasValue)
                .Select(t => EF.Functions.DateDiffHour(t.CreatedAt, t.CompletedAt!.Value))
                .ToListAsync();

            var avgCompletionTimeHours = completedWithTime.Any()
                ? completedWithTime.Average()
                : 0.0;

            // Tasks per user breakdown (using GroupBy to prevent translation errors and improve efficiency)
            var userTaskData = await _context.Tasks
                .Where(t => t.AssignedTo != null)
                .GroupBy(t => new { t.AssignedTo, t.Assignee!.Name })
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

            var analytics = new SystemAnalyticsDto(
                totalUsers,
                totalWorkflows,
                activeWorkflows,
                pendingTasks,
                inProgressTasks,
                completedTasks,
                overdueTasks,
                Math.Round(avgCompletionTimeHours, 2),
                tasksPerUser
            );

            return Ok(analytics);
        }

        // GET: api/Report/audit-logs — Latest system activity logs for compliance
        [HttpGet("audit-logs")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var total = await _context.AuditLogs.CountAsync();

            var logs = await _context.AuditLogs
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Join(_context.Users,
                      log => log.UserId,
                      user => user.UserId,
                      (log, user) => new AuditLogResponse(user.Name, log.Action, log.EntityName, log.Timestamp))
                .ToListAsync();

            return Ok(new { total, page, pageSize, data = logs });
        }

        // GET: api/Report/overdue-tasks — All overdue tasks for manager review
        [HttpGet("overdue-tasks")]
        [Authorize(Roles = "Admin,Manager,Auditor")]
        public async Task<IActionResult> GetOverdueTasks()
        {
            var now = DateTime.UtcNow;

            var tasks = await _context.Tasks
                .Include(t => t.Workflow)
                .Include(t => t.Assignee)
                .Where(t =>
                    t.DueDate.HasValue &&
                    t.DueDate < now &&
                    t.Status != "Completed" &&
                    t.Status != "Cancelled")
                .OrderBy(t => t.DueDate)
                .Select(t => new {
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

            return Ok(tasks);
        }
    }
}