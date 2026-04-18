using System;
using System.Collections.Generic;

namespace SmartWorkFlowX.Domain.Entities
{
    public class Workflow
    {
        public int WorkflowId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CreatedBy { get; set; }
        public string Status { get; set; } = "Draft"; // Draft | Active | Inactive
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public User? Creator { get; set; }
        public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
    }

    public class WorkflowStep
    {
        public int StepId { get; set; }
        public int WorkflowId { get; set; }
        public int StepOrder { get; set; }               // e.g., 1, 2, 3
        public string StepName { get; set; } = string.Empty; // e.g., "Manager Review"
        public string? Description { get; set; }          // Instructions for approver
        public int ApproverRoleId { get; set; }           // Role required to approve this step
        public string OnRejectAction { get; set; } = "Cancel"; // "GoBack" | "Cancel"
        public int? EscalationHours { get; set; }         // null = no escalation

        // Navigation Properties
        public Role? ApproverRole { get; set; }
    }
}