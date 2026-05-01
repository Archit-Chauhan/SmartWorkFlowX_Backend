using SmartWorkFlowX.Application.Dtos;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Application service contract for Reporting use-cases.
    /// </summary>
    public interface IReportService
    {
        Task<SystemAnalyticsDto> GetAnalyticsAsync();
        Task<PaginatedList<AuditLogResponse>> GetAuditLogsAsync(int page, int pageSize);
        Task<List<object>> GetOverdueTasksAsync();
    }
}


