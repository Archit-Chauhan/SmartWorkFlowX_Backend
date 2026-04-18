namespace SmartWorkFlowX.Domain.Entities
{
    public class TaskStepHistory
    {
        public int Id { get; set; }
        public int TaskId { get; set; }
        public int StepOrder { get; set; }
        public int ActedByUserId { get; set; }
        public string Action { get; set; } = string.Empty;  // "Approved" | "Rejected"
        public string? Comment { get; set; }
        public DateTime ActedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public TaskItem? Task { get; set; }
        public User? ActedByUser { get; set; }
    }
}
