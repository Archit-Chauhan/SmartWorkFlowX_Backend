namespace SmartWorkFlowX.Application.Dtos
{
    // --- Request DTOs ---

    public record TaskCreateRequest(
        string Title,
        string Description,
        int WorkflowId,
        int AssignedTo,
        string Priority,     // Low | Medium | High
        DateTime? DueDate
    );

    public record TaskStatusUpdateRequest(string Status);

    public record TaskRejectRequest(string Reason, string? Comment);

    // --- Response DTOs ---

    public record TaskStepHistoryResponse(
        int StepOrder,
        string ActedByName,
        string Action,
        string? Comment,
        DateTime ActedAt
    );
}