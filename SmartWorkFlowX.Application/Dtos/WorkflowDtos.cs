using System.ComponentModel.DataAnnotations;

namespace SmartWorkFlowX.Application.Dtos
{
    // --- Request DTOs ---

    public record WorkflowStepCreateDto(
        [Required] int StepOrder,
        [Required] int ApproverRoleId,
        [Required] string StepName,
        string? Description,
        [Required] string OnRejectAction,   // "GoBack" | "Cancel"
        int? EscalationHours
    );

    public record WorkflowCreateRequest(
        [Required] string Title,
        string Description,
        [Required] List<WorkflowStepCreateDto> Steps
    );

    public record WorkflowUpdateRequest(
        [Required] string Title,
        string Description,
        [Required] string Status,           // Draft | Active | Inactive
        [Required] List<WorkflowStepCreateDto> Steps
    );

    // --- Response DTOs ---

    public record WorkflowStepResponse(
        int StepId,
        int StepOrder,
        string StepName,
        string? Description,
        string ApproverRoleName,
        string OnRejectAction,
        int? EscalationHours
    );

    public record WorkflowResponse(
        int WorkflowId,
        string Title,
        string Status,
        int StepCount
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

