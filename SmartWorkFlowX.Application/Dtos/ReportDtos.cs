namespace SmartWorkFlowX.Application.Dtos
{
    public record SystemAnalyticsDto(
        int TotalUsers,
        int TotalWorkflows,
        int ActiveWorkflows,
        int PendingTasks,
        int InProgressTasks,
        int CompletedTasks,
        int OverdueTasks,
        double AvgCompletionTimeHours,
        List<TasksPerUserDto> TasksPerUser
    );

    public record TasksPerUserDto(
        string UserName,
        int PendingCount,
        int InProgressCount,
        int CompletedCount
    );

    public record AuditLogResponse(
        string UserName,
        string Action,
        string EntityName,
        DateTime Timestamp
    );
}

