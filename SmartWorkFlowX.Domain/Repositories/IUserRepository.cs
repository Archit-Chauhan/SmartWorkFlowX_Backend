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
        Task<User?> GetByEmailWithRoleAsync(string email);
        Task<IEnumerable<User>> GetAllWithRolesAsync(string? search = null);
        /// <summary>Active (not soft-deleted) users with their Role, ordered by Name.</summary>
        Task<List<User>> GetActiveWithRolesAsync();
        /// <summary>True when the user exists and is not soft-deleted.</summary>
        Task<bool> ActiveUserExistsAsync(int userId);
        Task<(IEnumerable<User> users, int total)> GetPaginatedAsync(int page, int pageSize, string? search = null);
        Task<bool> EmailExistsAsync(string email);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task SoftDeleteAsync(int userId);
        Task RestoreAsync(int userId);
        Task SaveAsync();
    }
}
