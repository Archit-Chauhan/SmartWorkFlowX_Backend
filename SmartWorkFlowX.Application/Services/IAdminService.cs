using SmartWorkFlowX.Application.Dtos;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Application service contract for Admin user management use-cases.
    /// </summary>
    public interface IAdminService
    {
        Task<List<object>> GetAllUsersAsync(string? search = null);
        Task<PaginatedList<object>> GetPaginatedUsersAsync(int page, int limit, string? search = null);
        Task<List<object>> GetAllRolesAsync();
        Task<int> CreateUserAsync(UserCreateRequest request, int actingUserId);
        Task DeleteUserAsync(int targetUserId, int actingUserId);
    }
}


