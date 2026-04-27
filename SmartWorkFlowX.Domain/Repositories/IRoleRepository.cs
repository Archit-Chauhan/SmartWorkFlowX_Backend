using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Domain.Repositories
{
    /// <summary>
    /// Repository contract for Role lookups.
    /// </summary>
    public interface IRoleRepository
    {
        Task<List<Role>> GetAllAsync();
    }
}
