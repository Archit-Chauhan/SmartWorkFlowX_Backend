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
        Task<(IEnumerable<TaskItem> tasks, int total)> GetMyTasksPaginatedAsync(int userId, int page, int pageSize);
        Task<List<TaskItem>> GetAllFilteredAsync(string? status, string? priority, int? assignedTo, int? categoryId);
        Task<AllTasksPage> GetAllTasksPagedAsync(AllTasksQuery query);
        Task<List<TaskStepHistory>> GetHistoryAsync(int taskId);
        Task<List<TaskItem>> GetMyActivityAsync(int userId);
        Task<(IEnumerable<TaskItem> tasks, int total)> GetMyActivityPaginatedAsync(int userId, int page, int pageSize);
        Task<bool> RoleHasUsersAsync(int roleId);
        Task<int?> GetUserRoleIdAsync(int userId);
        Task AddAsync(TaskItem task);
        Task AddHistoryAsync(TaskStepHistory history);
        Task SaveAsync();
    }
}
