using SmartWorkFlowX.Domain.Common;

namespace SmartWorkFlowX.Domain.Entities
{
    public class TaskItem : ISoftDeletable
    {
        public int TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int WorkflowId { get; set; }
        public int? AssignedTo { get; set; }
        public string Status { get; set; } = "Pending"; // Pending | In Progress | Completed | Cancelled | Rejected
        public string Priority { get; set; } = "Medium"; // Low | Medium | High
        public int CurrentStepOrder { get; set; } = 1;
        public string? RejectedReason { get; set; }      // Set when a step is rejected
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }        // Set when status transitions to Completed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft Delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        // Navigation
        public Workflow? Workflow { get; set; }
        public User? Assignee { get; set; }
        public ICollection<TaskStepHistory> StepHistories { get; set; } = new List<TaskStepHistory>();
    }
}