namespace SmartWorkFlowX.Domain.Repositories
{
    /// <summary>
    /// A validated, normalised query for the admin/manager "All Tasks" table.
    /// Built by the Application layer (AllTasksQueryParser); executed by the repository.
    /// </summary>
    public class AllTasksQuery
    {
        /// <summary>Trimmed search text (already length-checked), or null for no search.</summary>
        public string? Search { get; set; }

        /// <summary>
        /// The statuses the result may contain: the tab (group) intersected with the optional exact status.
        /// Never null; may be empty (then nothing can match).
        /// </summary>
        public List<string> Statuses { get; set; } = new List<string>();

        public string? Priority { get; set; }
        public int? CategoryId { get; set; }
        public int? AssignedTo { get; set; }

        /// <summary>True = only Pending / In Progress tasks whose DueDate is before <see cref="Now"/>.</summary>
        public bool Overdue { get; set; }

        /// <summary>due | created | priority | title | workflow | assignee | status</summary>
        public string Sort { get; set; } = "due";
        public bool Descending { get; set; }

        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;

        /// <summary>The clock used for the overdue filter (UTC).</summary>
        public DateTime Now { get; set; }
    }

    /// <summary>One row of the All Tasks table (flat projection, no entity graph).</summary>
    public class AllTasksRow
    {
        public int TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int WorkflowId { get; set; }
        public string? WorkflowTitle { get; set; }
        public int? TotalSteps { get; set; }
        public int CurrentStepOrder { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? RejectedReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? AssignedTo { get; set; }
        public string? AssigneeName { get; set; }
        public string? AssignedRoleName { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryColor { get; set; }
    }

    /// <summary>Tab counts: rows matching every filter except group/status.</summary>
    public class AllTasksCounts
    {
        public const string StatusPending = "Pending";
        public const string StatusInProgress = "In Progress";
        public const string StatusCompleted = "Completed";
        public const string StatusRejected = "Rejected";
        public const string StatusCancelled = "Cancelled";

        public static readonly string[] OpenStatuses = { StatusPending, StatusInProgress };
        public static readonly string[] CompletedStatuses = { StatusCompleted };
        public static readonly string[] ClosedStatuses = { StatusRejected, StatusCancelled };

        public int All { get; set; }
        public int Open { get; set; }
        public int Completed { get; set; }
        public int Closed { get; set; }

        /// <summary>Sum of the per-status counts for the given statuses (missing statuses count as 0).</summary>
        public static int Sum(IDictionary<string, int> byStatus, IEnumerable<string> statuses)
        {
            int total = 0;
            foreach (string status in statuses)
            {
                int count;
                if (byStatus.TryGetValue(status, out count)) total += count;
            }
            return total;
        }

        /// <summary>Builds the tab counts from a status -> count map. all = open + completed + closed.</summary>
        public static AllTasksCounts FromStatusCounts(IDictionary<string, int> byStatus)
        {
            int open = Sum(byStatus, OpenStatuses);
            int completed = Sum(byStatus, CompletedStatuses);
            int closed = Sum(byStatus, ClosedStatuses);
            return new AllTasksCounts
            {
                Open = open,
                Completed = completed,
                Closed = closed,
                All = open + completed + closed
            };
        }
    }

    /// <summary>What the repository returns for one page.</summary>
    public class AllTasksPage
    {
        public List<AllTasksRow> Rows { get; set; } = new List<AllTasksRow>();
        public int Total { get; set; }
        public AllTasksCounts Counts { get; set; } = new AllTasksCounts();
    }
}
