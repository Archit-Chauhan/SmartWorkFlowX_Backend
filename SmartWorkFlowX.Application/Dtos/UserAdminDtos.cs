using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Application.Dtos
{
    /// <summary>Paging envelope of GET /api/Admin/users plus the status chip counts.</summary>
    public class UsersPagedResponse : PaginatedList<object>
    {
        public UserListCounts Counts { get; set; } = new UserListCounts();
    }

    /// <summary>Body of PUT /api/Admin/users/{id}/role.</summary>
    public record ChangeUserRoleRequest(int RoleId);
}
