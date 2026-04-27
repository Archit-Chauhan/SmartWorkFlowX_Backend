using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Domain.Repositories
{
    /// <summary>
    /// Repository contract for AuditLog persistence and querying.
    /// Defined in the Domain layer — Infrastructure implements this.
    /// GetPagedWithUserAsync returns AuditLog entities with their User
    /// navigation property included so the Application layer can project.
    /// </summary>
    public interface IAuditLogRepository
    {
        Task AddAsync(AuditLog log);
        Task<(List<AuditLog> Items, int Total)> GetPagedWithUserAsync(int page, int pageSize);
        Task SaveAsync();
    }
}
