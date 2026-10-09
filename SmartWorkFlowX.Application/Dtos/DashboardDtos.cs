namespace SmartWorkFlowX.Application.Dtos
{
    // ── Request ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Query string of GET /Report/dashboard and /Report/dashboard/export.
    /// Dates are yyyy-MM-dd, inclusive. Missing dates default to the last 30 days.
    /// </summary>
    public class DashboardRequest
    {
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public int? CategoryId { get; set; }
        public int? WorkflowId { get; set; }
        public int? AssigneeId { get; set; }
    }

    // ── Response (camelCase JSON; mirrors frontend src/models/Dashboard.ts) ──

    /// <summary>Current value plus the same measure over the previous period of equal length (null when it cannot be computed).</summary>
    public record KpiValueDto(double Current, double? Previous);

    public record DashboardKpisDto(
        KpiValueDto Created,
        KpiValueDto Completed,
        KpiValueDto Open,
        KpiValueDto Overdue,
        KpiValueDto OnTimeRatePct,
        KpiValueDto AvgCompletionHours
    );

    public record TrendPointDto(string Date, int Created, int Completed);

    public record DashboardRangeDto(string From, string To, string PreviousFrom, string PreviousTo, string Bucket);

    public record StatusCountDto(string Status, int Count);

    public record PriorityCountDto(string Priority, int Count);

    public record CategoryCountDto(int? CategoryId, string Name, string ColorHex, int Count);

    public record WorkflowStatDto(int WorkflowId, string Title, int Total, int Completed, double? AvgCycleHours);

    public record WorkloadRowDto(int UserId, string Name, int Pending, int InProgress, int Completed);

    public record AgingBucketDto(string Bucket, int Count);

    public record OverdueTaskDto(
        int TaskId,
        string Title,
        string? AssigneeName,
        string? WorkflowTitle,
        string Priority,
        string DueDate,
        int DaysOverdue
    );

    public record OrgTotalsDto(int Users, int Workflows, int ActiveWorkflows);

    public record WorkflowOptionDto(int Id, string Title);

    public record CategoryOptionDto(int Id, string Name, string ColorHex);

    public record AssigneeOptionDto(int Id, string Name);

    public record DashboardOptionsDto(
        List<WorkflowOptionDto> Workflows,
        List<CategoryOptionDto> Categories,
        List<AssigneeOptionDto> Assignees
    );

    public record DashboardResponseDto(
        string Scope,                       // "all" | "self"
        string GeneratedAt,
        List<string> Permissions,
        OrgTotalsDto? Totals,               // only with the "org-totals" permission
        DashboardRangeDto Range,
        DashboardKpisDto Kpis,
        List<TrendPointDto> Series,
        List<StatusCountDto> ByStatus,
        List<PriorityCountDto> ByPriority,
        List<CategoryCountDto> ByCategory,
        List<WorkflowStatDto> ByWorkflow,
        List<WorkloadRowDto> Workload,      // empty without the "workload" permission
        List<AgingBucketDto> OverdueAging,
        List<OverdueTaskDto> TopOverdue,
        DashboardOptionsDto Options
    );

    /// <summary>Result of the tasks CSV export.</summary>
    public record DashboardCsvResult(string Content, string FileName, int RowCount);

    // ── Data handed from the repository to the calculator ───────────────────

    /// <summary>The columns of a task the dashboard needs. Times are UTC.</summary>
    public class DashboardTaskRow
    {
        public int TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int WorkflowId { get; set; }
        public string? WorkflowTitle { get; set; }
        public int? AssignedTo { get; set; }
        public int? OriginalAssignedTo { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryColor { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class DashboardUserRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
    }

    public class DashboardWorkflowRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class DashboardCategoryRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ColorHex { get; set; } = string.Empty;
    }

    /// <summary>Reference data for names, filter dropdowns and totals.</summary>
    public class DashboardLookups
    {
        public List<DashboardUserRow> Users { get; set; } = new List<DashboardUserRow>();
        public List<DashboardWorkflowRow> Workflows { get; set; } = new List<DashboardWorkflowRow>();
        public List<DashboardCategoryRow> Categories { get; set; } = new List<DashboardCategoryRow>();
    }
}
