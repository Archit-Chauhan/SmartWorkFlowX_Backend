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
        /// <summary>True if another non-deleted workflow has this title (case-insensitive). Pass the already-trimmed title.</summary>
        Task<bool> TitleExistsAsync(string trimmedTitle, int? excludeWorkflowId);
        /// <summary>Tasks with Status Pending or In Progress per workflow id, in one grouped query. Ids with none are absent.</summary>
        Task<Dictionary<int, int>> GetActiveTaskCountsAsync(IEnumerable<int> workflowIds);
        Task AddAsync(Workflow workflow);
        void RemoveSteps(IEnumerable<WorkflowStep> steps);
        Task SaveAsync();
    }
}
