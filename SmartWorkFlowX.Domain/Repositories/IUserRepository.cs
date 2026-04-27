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
        Task<List<User>> GetAllWithRolesAsync();
        Task<bool> EmailExistsAsync(string email);
        Task AddAsync(User user);
        void Remove(User user);
        Task SaveAsync();
    }
}
