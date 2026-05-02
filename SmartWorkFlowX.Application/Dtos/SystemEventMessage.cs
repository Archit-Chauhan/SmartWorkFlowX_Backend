namespace SmartWorkFlowX.Application.Dtos
{
    public class SystemEventMessage
    {
        public string EventType { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string ActionDescription { get; set; } = string.Empty;
        public int ActedByUserId { get; set; }
        
        // Optional payload for notifications
        public int? TargetUserId { get; set; }
        public int? TargetRoleId { get; set; }
        public string NotificationMessage { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
