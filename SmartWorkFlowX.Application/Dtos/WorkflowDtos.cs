namespace SmartWorkFlowX.Application.Dtos
{
    // --- Request DTOs ---

    public record WorkflowStepCreateDto(
        int StepOrder,
        int ApproverRoleId,
        string StepName,
        string? Description,
        string OnRejectAction,   // "GoBack" | "Cancel"
        int? EscalationHours
    );

    public record WorkflowCreateRequest(
        string Title,
        string Description,
        List<WorkflowStepCreateDto> Steps
    );

    public record WorkflowUpdateRequest(
        string Title,
        string Description,
        string Status,           // Draft | Active | Inactive
        List<WorkflowStepCreateDto> Steps
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