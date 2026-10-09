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

        /// <summary>
        /// The columns of every task that can affect a dashboard for the range: tasks created from
        /// <paramref name="previousStart"/> up to <paramref name="end"/>, plus older tasks that were completed in or after
        /// the previous period or are still open. When <paramref name="scopedUserId"/> is set, only that user's tasks
        /// (assigned to, originally assigned to, or acted on by them) are returned, so out-of-scope rows never leave the database.
        /// </summary>
        Task<List<DashboardTaskRow>> GetDashboardTaskRowsAsync(DateTime previousStart, DateTime end, int? scopedUserId);

        /// <summary>Users, workflows and categories used for names, filter dropdowns and totals.</summary>
        Task<DashboardLookups> GetDashboardLookupsAsync();
    }
}




