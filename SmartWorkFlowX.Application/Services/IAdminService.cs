using SmartWorkFlowX.Application.Dtos;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Application service contract for Admin user management use-cases.
    /// </summary>
    public interface IAdminService
    {
        Task<List<object>> GetAllUsersAsync(string? search = null, string? status = null, int? roleId = null);
        Task<UsersPagedResponse> GetPaginatedUsersAsync(int page, int limit, string? search = null, string? status = null, int? roleId = null, string? sort = null, string? dir = null);
        Task<List<object>> GetAllRolesAsync();
        Task<int> CreateUserAsync(UserCreateRequest request, int actingUserId);
        Task DeleteUserAsync(int targetUserId, int actingUserId);
        Task RestoreUserAsync(int targetUserId, int actingUserId);

        /// <summary>Changes a user's role. Returns false (and writes nothing) when the role is already that role.</summary>
        Task<bool> ChangeUserRoleAsync(int targetUserId, int newRoleId, int actingUserId);
    }
}
