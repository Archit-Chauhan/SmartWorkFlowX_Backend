using System;
using System.Collections.Generic;

namespace SmartWorkFlowX.Infrastructure.Data;

public partial class WorkflowStep
{
    public int StepId { get; set; }

    public int WorkflowId { get; set; }

    public int StepOrder { get; set; }

    public int ApproverRoleId { get; set; }

    public virtual Role ApproverRole { get; set; } = null!;

    public virtual Workflow Workflow { get; set; } = null!;
}
