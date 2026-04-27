using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Domain.Repositories
{
    /// <summary>
    /// Repository contract for Notification persistence and querying.
    /// Defined in the Domain layer — Infrastructure implements this.
    /// </summary>
    public interface INotificationRepository
    {
        Task<(List<Notification> Items, int Total)> GetPagedAsync(int userId, int page, int pageSize);
        Task<int> GetUnreadCountAsync(int userId);
        Task<Notification?> GetByIdAsync(int notificationId, int userId);
        Task<List<Notification>> GetUnreadAsync(int userId);
        Task SaveAsync();
    }
}
