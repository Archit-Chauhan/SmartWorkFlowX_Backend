using SmartWorkFlowX.Application.Dtos;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Application service contract for Reporting use-cases.
    /// </summary>
    public interface IReportService
    {
        Task<SystemAnalyticsDto> GetAnalyticsAsync();
        Task<PaginatedList<AuditLogResponse>> GetAuditLogsAsync(int page, int pageSize, string? search = null);
        Task<List<AuditLogResponse>> GetAllAuditLogsAsync(string? search = null);
        Task<List<object>> GetOverdueTasksAsync();

        /// <summary>Dashboard numbers for the signed-in user. The scope and the sections come from the role, never from the request.</summary>
        Task<DashboardResponseDto> GetDashboardAsync(DashboardRequest request, int userId, string? role);

        /// <summary>Tasks CSV for the same filters and scope. Writes an audit-log entry.</summary>
        Task<DashboardCsvResult> ExportDashboardTasksAsync(DashboardRequest request, int userId, string? role);
    }
}


