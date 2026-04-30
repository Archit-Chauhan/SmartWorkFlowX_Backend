using SmartWorkFlowX.Application.Dtos;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Application service contract for Workflow use-cases.
    /// </summary>
    public interface IWorkflowService
    {
        Task<List<WorkflowResponse>> GetAllAsync();
        Task<PaginatedList<WorkflowResponse>> GetPaginatedAsync(int page, int pageSize);
        Task<WorkflowDetailResponse> GetByIdAsync(int workflowId);
        Task<int> CreateAsync(WorkflowCreateRequest request, int createdByUserId);
        Task UpdateAsync(int workflowId, WorkflowUpdateRequest request, int actingUserId);
        Task DeactivateAsync(int workflowId, int actingUserId);
        Task<int> CloneAsync(int workflowId, int actingUserId);
    }
}


