using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Domain.Repositories
{
    public interface ITaskCategoryRepository
    {
        Task<List<TaskCategory>> GetAllActiveAsync();
        Task SaveAsync();
    }
}
