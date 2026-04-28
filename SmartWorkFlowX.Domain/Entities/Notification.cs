using SmartWorkFlowX.Domain.Common;

namespace SmartWorkFlowX.Domain.Entities
{
    public class Notification : ISoftDeletable
    {
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft Delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // Navigation
        public User? User { get; set; }
    }
}