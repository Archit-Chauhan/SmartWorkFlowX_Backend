using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Services;
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
                .Include(t => t.Category)
                .FirstOrDefaultAsync(t => t.TaskId == taskId);

        public async Task<List<TaskItem>> GetMyTasksAsync(int userId)
            => await _context.Tasks
                .Include(t => t.Workflow)
                .Include(t => t.Category)
                .Where(t => (t.AssignedTo == userId
                             || (t.AssignedTo == null && t.AssignedRoleId != null
                                 && t.AssignedRoleId == _context.Users.Where(u => u.UserId == userId).Select(u => (int?)u.RoleId).FirstOrDefault()))
                            && t.Status != "Completed" && t.Status != "Cancelled")
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

        public async Task<List<TaskItem>> GetAllFilteredAsync(string? status, string? priority, int? assignedTo, int? categoryId)
        {
            var query = _context.Tasks
                .Include(t => t.Workflow)
                .Include(t => t.Assignee)
                .Include(t => t.Category)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.Status == status);

            if (!string.IsNullOrEmpty(priority))
                query = query.Where(t => t.Priority == priority);

            if (assignedTo.HasValue)
                query = query.Where(t => t.AssignedTo == assignedTo);

            if (categoryId.HasValue)
                query = query.Where(t => t.CategoryId == categoryId);

            return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
        }

        public async Task<AllTasksPage> GetAllTasksPagedAsync(AllTasksQuery q)
        {
            // Filters shared by the rows and the tab counts (everything except group/status).
            var filtered = _context.Tasks.AsNoTracking().AsQueryable();

            if (q.Priority != null)
                filtered = filtered.Where(t => t.Priority == q.Priority);

            if (q.CategoryId.HasValue)
                filtered = filtered.Where(t => t.CategoryId == q.CategoryId);

            if (q.AssignedTo.HasValue)
                filtered = filtered.Where(t => t.AssignedTo == q.AssignedTo);

            if (q.Overdue)
            {
                var now = q.Now;
                filtered = filtered.Where(t =>
                    (t.Status == "Pending" || t.Status == "In Progress")
                    && t.DueDate != null && t.DueDate < now);
            }

            if (!string.IsNullOrEmpty(q.Search))
            {
                var pattern = "%" + AllTasksQueryParser.EscapeLike(q.Search) + "%";
                filtered = filtered.Where(t =>
                    EF.Functions.Like(t.Title, pattern, "\\")
                    || EF.Functions.Like(t.Description!, pattern, "\\")
                    || EF.Functions.Like(t.Workflow!.Title, pattern, "\\")
                    || EF.Functions.Like(t.Assignee!.Name, pattern, "\\"));
            }

            // One grouped query gives every tab count; the page total is derived from it.
            var grouped = await filtered
                .GroupBy(t => t.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var byStatus = new Dictionary<string, int>();
            foreach (var g in grouped) byStatus[g.Status] = g.Count;

            var result = new AllTasksPage
            {
                Counts = AllTasksCounts.FromStatusCounts(byStatus),
                Total = AllTasksCounts.Sum(byStatus, q.Statuses)
            };

            int skip = (q.Page - 1) * q.Limit;
            if (q.Statuses.Count == 0 || skip >= result.Total)
                return result;   // nothing can match, or the page is past the end

            var statuses = q.Statuses;
            var ordered = ApplyAllTasksOrder(filtered.Where(t => statuses.Contains(t.Status)), q);

            result.Rows = await ordered
                .Skip(skip)
                .Take(q.Limit)
                .Select(t => new AllTasksRow
                {
                    TaskId = t.TaskId,
                    Title = t.Title,
                    Description = t.Description,
                    WorkflowId = t.WorkflowId,
                    WorkflowTitle = t.Workflow != null ? t.Workflow.Title : null,
                    TotalSteps = t.Workflow != null ? (int?)t.Workflow.Steps.Count() : null,
                    CurrentStepOrder = t.CurrentStepOrder,
                    Status = t.Status,
                    Priority = t.Priority,
                    DueDate = t.DueDate,
                    CompletedAt = t.CompletedAt,
                    RejectedReason = t.RejectedReason,
                    CreatedAt = t.CreatedAt,
                    AssignedTo = t.AssignedTo,
                    AssigneeName = t.Assignee != null ? t.Assignee.Name : null,
                    AssignedRoleName = (t.AssignedTo == null && t.AssignedRoleId != null)
                        ? _context.Roles.Where(r => r.RoleId == t.AssignedRoleId).Select(r => r.RoleName).FirstOrDefault()
                        : null,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category != null ? t.Category.Name : null,
                    CategoryColor = t.Category != null ? t.Category.ColorHex : null
                })
                .ToListAsync();

            return result;
        }

        // Stable ordering: the chosen key, then CreatedAt desc, then TaskId asc (so paging never repeats or skips rows).
        private static IOrderedQueryable<TaskItem> ApplyAllTasksOrder(IQueryable<TaskItem> query, AllTasksQuery q)
        {
            IOrderedQueryable<TaskItem> ordered;
            bool desc = q.Descending;

            switch (q.Sort)
            {
                case "created":
                    ordered = desc ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt);
                    break;
                case "priority":
                    // High = 0, Medium = 1, Low = 2; ascending puts High first
                    ordered = desc
                        ? query.OrderByDescending(t => t.Priority == "High" ? 0 : (t.Priority == "Medium" ? 1 : 2))
                        : query.OrderBy(t => t.Priority == "High" ? 0 : (t.Priority == "Medium" ? 1 : 2));
                    break;
                case "title":
                    ordered = desc ? query.OrderByDescending(t => t.Title) : query.OrderBy(t => t.Title);
                    break;
                case "workflow":
                    ordered = desc ? query.OrderByDescending(t => t.Workflow!.Title) : query.OrderBy(t => t.Workflow!.Title);
                    break;
                case "assignee":
                    // tasks nobody holds always last, in both directions
                    ordered = desc
                        ? query.OrderBy(t => t.Assignee == null).ThenByDescending(t => t.Assignee!.Name)
                        : query.OrderBy(t => t.Assignee == null).ThenBy(t => t.Assignee!.Name);
                    break;
                case "status":
                    ordered = desc ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status);
                    break;
                default:
                    // "due": tasks without a due date always last, in both directions
                    ordered = desc
                        ? query.OrderBy(t => t.DueDate == null).ThenByDescending(t => t.DueDate)
                        : query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate);
                    break;
            }

            return ordered.ThenByDescending(t => t.CreatedAt).ThenBy(t => t.TaskId);
        }

        public async Task<List<TaskStepHistory>> GetHistoryAsync(int taskId)
            => await _context.TaskStepHistories
                .Include(h => h.ActedByUser)
                .Where(h => h.TaskId == taskId)
                .OrderBy(h => h.StepOrder)
                .ThenBy(h => h.ActedAt)
                .ToListAsync();

        public async Task<bool> RoleHasUsersAsync(int roleId)
            => await _context.Users.AnyAsync(u => u.RoleId == roleId);

        public async Task<int?> GetUserRoleIdAsync(int userId)
            => await _context.Users
                .Where(u => u.UserId == userId)
                .Select(u => (int?)u.RoleId)
                .FirstOrDefaultAsync();

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
                .Include(t => t.Category)
                .Where(t => actedTaskIds.Contains(t.TaskId) && t.AssignedTo != userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<(IEnumerable<TaskItem> tasks, int total)> GetMyTasksPaginatedAsync(int userId, int page, int pageSize)
        {
            var query = _context.Tasks
                .Include(t => t.Workflow)
                .Include(t => t.Category)
                .Where(t => (t.AssignedTo == userId
                             || (t.AssignedTo == null && t.AssignedRoleId != null
                                 && t.AssignedRoleId == _context.Users.Where(u => u.UserId == userId).Select(u => (int?)u.RoleId).FirstOrDefault()))
                            && t.Status != "Completed" && t.Status != "Cancelled");

            var total = await query.CountAsync();
            // Most urgent first: earliest due date, tasks without a due date last, then newest.
            // The My Tasks screen groups by urgency and walks this order in review mode, so it must be stable across pages.
            var items = await query
                .OrderBy(t => t.DueDate == null)
                .ThenBy(t => t.DueDate)
                .ThenByDescending(t => t.CreatedAt)
                .ThenBy(t => t.TaskId)
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
                .Include(t => t.Category)
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
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new DbUpdateConcurrencyException("This task was already handled by another user. Please refresh.");
            }
        }
    }
}
