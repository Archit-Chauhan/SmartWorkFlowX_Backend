using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Domain.Repositories
{
    /// <summary>
    /// Repository contract for Workflow and WorkflowStep persistence.
    /// Defined in the Domain layer — Infrastructure implements this.
    /// </summary>
    public interface IWorkflowRepository
    {
        Task<List<Workflow>> GetAllAsync();
        Task<(IEnumerable<Workflow> workflows, int total)> GetPaginatedAsync(int page, int pageSize);
        Task<Workflow?> GetByIdWithDetailsAsync(int workflowId);
        Task<Workflow?> GetByIdWithStepsAsync(int workflowId);
        Task<bool> HasActiveTasksAsync(int workflowId);
        Task AddAsync(Workflow workflow);
        void RemoveSteps(IEnumerable<WorkflowStep> steps);
        Task SaveAsync();
    }
}
