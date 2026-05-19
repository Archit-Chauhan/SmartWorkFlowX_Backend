using System.ComponentModel.DataAnnotations;

namespace SmartWorkFlowX.Application.Dtos
{
    // --- Request DTOs ---

    public record TaskCreateRequest(
        [Required] string Title,
        string Description,
        [Required] int WorkflowId,
        [Required] int AssignedTo,
        [Required] string Priority,     // Low | Medium | High
        DateTime? DueDate
    );

    public record TaskStatusUpdateRequest([Required] string Status);

    public record TaskRejectRequest([Required] string Reason, string? Comment);

    // --- Response DTOs ---

    public record TaskStepHistoryResponse(
        int StepOrder,
        string ActedByName,
        string Action,
        string? Comment,
        DateTime ActedAt
    );
}

