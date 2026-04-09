using System;
using System.Collections.Generic;

namespace SmartWorkFlowX.Infrastructure.Data;

public partial class AuditLog
{
    public int LogId { get; set; }

    public int UserId { get; set; }

    public string? Action { get; set; }

    public string? EntityName { get; set; }

    public DateTime? Timestamp { get; set; }

    public virtual User User { get; set; } = null!;
}
