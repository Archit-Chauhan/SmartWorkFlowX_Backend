using System.ComponentModel.DataAnnotations;

namespace SmartWorkFlowX.Application.Dtos
{
    // --- Request DTOs ---

    // Text fields are deliberately nullable and carry no [Required]: the service-level
    // WorkflowRequestValidator owns these rules so the 400 messages are the contract's exact ones.
    public record WorkflowStepCreateDto(
        int StepOrder,                      // ignored: stored as 1..n by array position
        int ApproverRoleId,
        string? StepName,
        string? Description,
        string? OnRejectAction,             // "GoBack" | "Cancel"
        int? EscalationHours
    );

    public record WorkflowCreateRequest(
        string? Title,
        string? Description,
        List<WorkflowStepCreateDto>? Steps,
        string? Status = null               // optional: null/Draft = Draft, Active = create as Active
    );

    public record WorkflowUpdateRequest(
        string? Title,
        string? Description,
        string? Status,                     // Draft | Active | Inactive
        List<WorkflowStepCreateDto>? Steps
    );

    // --- Response DTOs ---

    public record WorkflowStepResponse(
        int StepId,
        int StepOrder,
        string StepName,
        string? Description,
        string ApproverRoleName,
        string OnRejectAction,
        int? EscalationHours,
        int ApproverRoleId = 0
    );

    public record WorkflowStepSummaryResponse(
        int StepOrder,
        string StepName,
        string ApproverRoleName
    );

    public record WorkflowResponse(
        int WorkflowId,
        string Title,
        string Status,
        int StepCount,
        string? Description = null,
        string CreatedByName = "",
        DateTime CreatedAt = default,
        int ActiveTaskCount = 0,
        List<WorkflowStepSummaryResponse>? Steps = null
    );

    public record WorkflowDetailResponse(
        int WorkflowId,
        string Title,
        string? Description,
        string Status,
        string CreatedByName,
        DateTime CreatedAt,
        List<WorkflowStepResponse> Steps
    );
}

