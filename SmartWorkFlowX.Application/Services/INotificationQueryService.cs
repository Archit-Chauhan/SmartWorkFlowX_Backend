namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Application service contract for reading and managing notifications.
    /// Separate from INotificationService (which handles sending/pushing).
    /// </summary>
    public interface INotificationQueryService
    {
        Task<(List<object> Items, int Total)> GetPagedAsync(int userId, int page, int pageSize);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task MarkAllAsReadAsync(int userId);
    }
}


