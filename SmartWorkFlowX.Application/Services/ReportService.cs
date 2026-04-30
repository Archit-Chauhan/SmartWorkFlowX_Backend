using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
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

        public async Task<PaginatedList<AuditLogResponse>> GetAuditLogsAsync(int page, int pageSize)
        {
            var (logs, total) = await _auditRepo.GetPagedWithUserAsync(page, pageSize);
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

        public async Task<List<object>> GetOverdueTasksAsync()
            => await _reportRepo.GetOverdueTasksAsync();
    }
}


