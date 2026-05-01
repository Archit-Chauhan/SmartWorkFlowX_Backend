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
        Task<IEnumerable<User>> GetAllWithRolesAsync();
        Task<(IEnumerable<User> users, int total)> GetPaginatedAsync(int page, int pageSize);
        Task<bool> EmailExistsAsync(string email);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task SoftDeleteAsync(int userId);
        Task SaveAsync();
    }
}
