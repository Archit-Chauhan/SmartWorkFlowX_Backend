using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Application.Services
{
    public class NotificationQueryService : INotificationQueryService
    {
        private readonly INotificationRepository _notificationRepo;

        public NotificationQueryService(INotificationRepository notificationRepo)
        {
            _notificationRepo = notificationRepo;
        }

        public async Task<(List<object> Items, int Total)> GetPagedAsync(int userId, int page, int pageSize)
        {
            var (notifications, total) = await _notificationRepo.GetPagedAsync(userId, page, pageSize);
            var items = notifications.Select(n => (object)new
            {
                n.NotificationId,
                n.Message,
                n.IsRead,
                n.CreatedAt
            }).ToList();
            return (items, total);
        }

        public async Task<int> GetUnreadCountAsync(int userId)
            => await _notificationRepo.GetUnreadCountAsync(userId);

        public async Task MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _notificationRepo.GetByIdAsync(notificationId, userId)
                ?? throw new KeyNotFoundException("Notification not found.");

            notification.IsRead = true;
            await _notificationRepo.SaveAsync();
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var unread = await _notificationRepo.GetUnreadAsync(userId);
            foreach (var n in unread)
                n.IsRead = true;

            await _notificationRepo.SaveAsync();
        }
    }
}


