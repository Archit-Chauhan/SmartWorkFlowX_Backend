using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Domain.Repositories
{
    /// <summary>
    /// Repository contract for TaskItem and TaskStepHistory persistence.
    /// Defined in the Domain layer — Infrastructure implements this.
    /// </summary>
    public interface ITaskRepository
    {
        Task<TaskItem?> GetByIdWithWorkflowAsync(int taskId);
        Task<List<TaskItem>> GetMyTasksAsync(int userId);
        Task<List<TaskItem>> GetAllFilteredAsync(string? status, string? priority, int? assignedTo);
        Task<List<TaskStepHistory>> GetHistoryAsync(int taskId);
        Task<User?> GetFirstUserByRoleAsync(int roleId);
        Task AddAsync(TaskItem task);
        Task AddHistoryAsync(TaskStepHistory history);
        Task SaveAsync();
    }
}
