namespace SmartWorkFlowX.Domain.Entities
{
    public class TaskCategory
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#6B7280";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
