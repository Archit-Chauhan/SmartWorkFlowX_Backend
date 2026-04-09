using System;
using System.Collections.Generic;

namespace SmartWorkFlowX.Infrastructure.Data;

public partial class Task
{
    public int TaskId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public int WorkflowId { get; set; }

    public int AssignedTo { get; set; }

    public string? Status { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User AssignedToNavigation { get; set; } = null!;

    public virtual Workflow Workflow { get; set; } = null!;
}
