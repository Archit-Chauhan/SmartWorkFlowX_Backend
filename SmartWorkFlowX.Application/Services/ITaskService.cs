using SmartWorkFlowX.Application.Dtos;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Application service contract for Task use-cases.
    /// </summary>
    public interface ITaskService
    {
        Task<List<object>> GetMyTasksAsync(int userId);
        Task<PaginatedList<object>> GetMyTasksPaginatedAsync(int userId, int page, int pageSize);
        Task<List<object>> GetAllFilteredAsync(string? status, string? priority, int? assignedTo);
        Task<int> AssignTaskAsync(TaskCreateRequest request, int actingUserId);
        Task<string> ApproveTaskAsync(int taskId, int actingUserId, string? comment);
        Task<string> RejectTaskAsync(int taskId, int actingUserId, TaskRejectRequest request);
        Task<List<TaskStepHistoryResponse>> GetHistoryAsync(int taskId);
        Task<List<object>> GetMyActivityAsync(int userId);
        Task<PaginatedList<object>> GetMyActivityPaginatedAsync(int userId, int page, int pageSize);
    }
}


