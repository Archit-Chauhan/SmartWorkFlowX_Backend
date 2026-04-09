using System;
using System.Collections.Generic;

namespace SmartWorkFlowX.Infrastructure.Data;

public partial class Report
{
    public int ReportId { get; set; }

    public string? Name { get; set; }

    public int GeneratedBy { get; set; }

    public DateTime? GeneratedAt { get; set; }

    public virtual User GeneratedByNavigation { get; set; } = null!;
}
