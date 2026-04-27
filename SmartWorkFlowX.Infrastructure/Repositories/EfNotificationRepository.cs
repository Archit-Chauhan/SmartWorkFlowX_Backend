using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Infrastructure.Repositories
{
    public class EfNotificationRepository : INotificationRepository
    {
        private readonly SmartWorkflowXDbContext _context;
        public EfNotificationRepository(SmartWorkflowXDbContext context) => _context = context;

        public async Task<(List<Notification> Items, int Total)> GetPagedAsync(int userId, int page, int pageSize)
        {
            var total = await _context.Notifications.CountAsync(n => n.UserId == userId);
            var items = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return (items, total);
        }

        public async Task<int> GetUnreadCountAsync(int userId)
            => await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public async Task<Notification?> GetByIdAsync(int notificationId, int userId)
            => await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);

        public async Task<List<Notification>> GetUnreadAsync(int userId)
            => await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}
