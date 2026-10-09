using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Application.Services
{
    public class ReportService : IReportService
    {
        private readonly IReportRepository _reportRepo;
        private readonly IAuditLogRepository _auditRepo;

        public ReportService(IReportRepository reportRepo, IAuditLogRepository auditRepo)
        {
            _reportRepo = reportRepo;
            _auditRepo = auditRepo;
        }

        public async Task<SystemAnalyticsDto> GetAnalyticsAsync()
            => await _reportRepo.GetAnalyticsAsync();

        public async Task<PaginatedList<AuditLogResponse>> GetAuditLogsAsync(int page, int pageSize, string? search = null)
        {
            var (logs, total) = await _auditRepo.GetPagedWithUserAsync(page, pageSize, search);
            var items = logs.Select(l => new AuditLogResponse(
                l.User?.Name ?? "Unknown",
                l.Action,
                l.EntityName,
                l.Timestamp)).ToList();

            return new PaginatedList<AuditLogResponse>
            {
                Data = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<List<AuditLogResponse>> GetAllAuditLogsAsync(string? search = null)
        {
            var logs = await _auditRepo.GetAllWithUserAsync(search);
            return logs.Select(l => new AuditLogResponse(
                l.User?.Name ?? "Unknown",
                l.Action,
                l.EntityName,
                l.Timestamp)).ToList();
        }

        public async Task<List<object>> GetOverdueTasksAsync()
            => await _reportRepo.GetOverdueTasksAsync();

        public async Task<DashboardResponseDto> GetDashboardAsync(DashboardRequest request, int userId, string? role)
        {
            DateTime now = DateTime.UtcNow;
            DashboardCriteria criteria = DashboardCalculator.ParseRequest(request, now);
            List<string> permissions = DashboardPolicy.PermissionsForRole(role);
            bool selfScope = !DashboardPolicy.SeesAllTasks(role);

            // The scope is decided here from the signed-in user, never from the query string.
            List<DashboardTaskRow> rows = await _reportRepo.GetDashboardTaskRowsAsync(
                criteria.PreviousStart, criteria.End, selfScope ? userId : (int?)null);
            DashboardLookups lookups = await _reportRepo.GetDashboardLookupsAsync();

            return DashboardCalculator.Build(rows, lookups, criteria, selfScope, permissions, now);
        }

        public async Task<DashboardCsvResult> ExportDashboardTasksAsync(DashboardRequest request, int userId, string? role)
        {
            List<string> permissions = DashboardPolicy.PermissionsForRole(role);
            if (!permissions.Contains(DashboardPermissions.ExportTasks))
                throw new UnauthorizedAccessException("You are not allowed to export tasks.");

            DateTime now = DateTime.UtcNow;
            DashboardCriteria criteria = DashboardCalculator.ParseRequest(request, now);
            bool selfScope = !DashboardPolicy.SeesAllTasks(role);

            List<DashboardTaskRow> rows = await _reportRepo.GetDashboardTaskRowsAsync(
                criteria.PreviousStart, criteria.End, selfScope ? userId : (int?)null);
            DashboardLookups lookups = await _reportRepo.GetDashboardLookupsAsync();

            DashboardCsvResult csv = DashboardCalculator.BuildTasksCsv(rows, lookups, criteria, permissions);

            // Accountability: every export is recorded.
            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = userId,
                Action = "Exported dashboard tasks CSV (" + csv.RowCount + " rows) for " + criteria.From + " to " + criteria.To + DescribeFilters(criteria),
                EntityName = "Reports",
                Timestamp = now
            });
            await _auditRepo.SaveAsync();

            return csv;
        }

        private static string DescribeFilters(DashboardCriteria c)
        {
            var parts = new List<string>();
            if (c.Status != null) parts.Add("status=" + c.Status);
            if (c.Priority != null) parts.Add("priority=" + c.Priority);
            if (c.CategoryId.HasValue) parts.Add("categoryId=" + c.CategoryId.Value);
            if (c.WorkflowId.HasValue) parts.Add("workflowId=" + c.WorkflowId.Value);
            if (c.AssigneeId.HasValue) parts.Add("assigneeId=" + c.AssigneeId.Value);
            return parts.Count == 0 ? string.Empty : " [" + string.Join(", ", parts) + "]";
        }
    }
}


