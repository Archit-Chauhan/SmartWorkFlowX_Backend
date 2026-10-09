using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Domain.Repositories
{
    /// <summary>
    /// Repository contract for User and Role persistence.
    /// Defined in the Domain layer — Infrastructure implements this.
    /// </summary>
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int userId);

        /// <summary>Finds a user by id including soft-deleted ones; the Role is loaded.</summary>
        Task<User?> GetByIdIncludingDeletedAsync(int userId);

        Task<User?> GetByEmailWithRoleAsync(string email);

        /// <summary>All matching users (soft-deleted included), deactivated last. Used by the CSV export.</summary>
        Task<IEnumerable<User>> GetAllWithRolesAsync(string? search, string status, int? roleId);

        /// <summary>One filtered, sorted page plus the total and the status counts (all in SQL).</summary>
        Task<UserListPage> GetUserListAsync(UserListQuery query);

        /// <summary>Open (Pending / In Progress) task count per assignee, for the given users only.</summary>
        Task<Dictionary<int, int>> GetOpenTaskCountsAsync(IReadOnlyCollection<int> userIds);

        /// <summary>Number of active (not deactivated) users whose role is Admin.</summary>
        Task<int> CountActiveAdminsAsync();

        Task<bool> EmailExistsAsync(string email);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task SoftDeleteAsync(int userId);
        Task RestoreAsync(int userId);
        Task SaveAsync();
    }
}
