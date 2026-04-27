using SmartWorkFlowX.Application.Dtos;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Repository contract for analytics queries that require complex
    /// EF Core-specific operations (DateDiff, GroupBy projections).
    /// Implemented in Infrastructure; defined in Domain so ReportService stays clean.
    /// </summary>
    public interface IReportRepository
    {
        Task<SystemAnalyticsDto> GetAnalyticsAsync();
        Task<List<object>> GetOverdueTasksAsync();
    }
}




