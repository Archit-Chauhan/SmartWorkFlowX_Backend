using System.Globalization;
using System.Text;
using SmartWorkFlowX.Application.Dtos;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>A validated dashboard request: the date range (UTC), the previous period and the optional filters.</summary>
    public class DashboardCriteria
    {
        public string From { get; set; } = string.Empty;       // yyyy-MM-dd
        public string To { get; set; } = string.Empty;         // yyyy-MM-dd
        public DateTime Start { get; set; }                    // from 00:00:00
        public DateTime End { get; set; }                      // to 23:59:59.9999999
        public int Days { get; set; }
        public DateTime PreviousStart { get; set; }
        public DateTime PreviousEnd { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public int? CategoryId { get; set; }
        public int? WorkflowId { get; set; }
        public int? AssigneeId { get; set; }
    }

    /// <summary>
    /// Pure dashboard maths (no database, no clock): every number the dashboard shows is defined here.
    /// Port of the frontend reference implementation (src/demo/dashboard.ts) and the metric definitions in
    /// docs/DASHBOARD_SPEC.md. All times are treated as UTC.
    /// </summary>
    public static class DashboardCalculator
    {
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
        private const int MaxRangeDays = 366;
        private const int DayBucketLimit = 45;
        private const int TopCount = 10;

        private static readonly string[] Statuses = { "Pending", "In Progress", "Completed", "Rejected", "Cancelled" };
        private static readonly string[] Priorities = { "High", "Medium", "Low" };
        private static readonly string[] AgingLabels = { "1-3 days", "4-7 days", "8-14 days", "15+ days" };

        // ── Request parsing ────────────────────────────────────────────────

        /// <summary>Validates the request. Missing dates default to the last 30 days ending today.</summary>
        public static DashboardCriteria ParseRequest(DashboardRequest request, DateTime now)
        {
            DateTime today = now.Date;
            DateTime to = today;
            DateTime from = today.AddDays(-29);

            bool hasTo = !string.IsNullOrWhiteSpace(request.To);
            bool hasFrom = !string.IsNullOrWhiteSpace(request.From);

            if (hasTo) to = ParseDay(request.To!, "to");
            if (hasFrom) from = ParseDay(request.From!, "from");
            else if (hasTo) from = to.AddDays(-29);

            if (from > to)
                throw new ArgumentException("'from' must not be after 'to'.");

            int days = (int)(to - from).TotalDays + 1;
            if (days > MaxRangeDays)
                throw new ArgumentException("The date range cannot exceed " + MaxRangeDays + " days.");

            return new DashboardCriteria
            {
                From = from.ToString(DateFormat, CultureInfo.InvariantCulture),
                To = to.ToString(DateFormat, CultureInfo.InvariantCulture),
                Start = from,
                End = to.AddDays(1).AddTicks(-1),
                Days = days,
                PreviousStart = from.AddDays(-days),
                PreviousEnd = from.AddTicks(-1),
                Status = CleanChoice(request.Status, Statuses, "status"),
                Priority = CleanChoice(request.Priority, Priorities, "priority"),
                CategoryId = request.CategoryId,
                WorkflowId = request.WorkflowId,
                AssigneeId = request.AssigneeId
            };
        }

        private static DateTime ParseDay(string value, string name)
        {
            DateTime parsed;
            if (!DateTime.TryParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                throw new ArgumentException("'" + name + "' must be a date in " + DateFormat + " format.");
            return parsed.Date;
        }

        /// <summary>Empty means "no filter"; anything else must be one of the known values (also keeps odd text out of the audit log).</summary>
        private static string? CleanChoice(string? value, string[] allowed, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string trimmed = value.Trim();
            foreach (string option in allowed)
            {
                if (string.Equals(option, trimmed, StringComparison.Ordinal)) return option;
            }
            throw new ArgumentException("Unknown " + name + " filter value.");
        }

        // ── Filtering and ownership ────────────────────────────────────────

        /// <summary>The person who owns the work: the current assignee, or the original employee once it has left them.</summary>
        public static int? OwnerOf(DashboardTaskRow t)
        {
            return t.AssignedTo ?? t.OriginalAssignedTo;
        }

        /// <summary>Applies the optional filters (not the date range). The assignee filter needs the 'assignee-filter' permission.</summary>
        public static List<DashboardTaskRow> Filter(IEnumerable<DashboardTaskRow> rows, DashboardCriteria c, IReadOnlyCollection<string> permissions)
        {
            bool assigneeAllowed = permissions.Contains(DashboardPermissions.AssigneeFilter) && c.AssigneeId.HasValue;
            var result = new List<DashboardTaskRow>();
            foreach (DashboardTaskRow t in rows)
            {
                if (c.Status != null && t.Status != c.Status) continue;
                if (c.Priority != null && t.Priority != c.Priority) continue;
                if (c.CategoryId.HasValue && t.CategoryId != c.CategoryId) continue;
                if (c.WorkflowId.HasValue && t.WorkflowId != c.WorkflowId.Value) continue;
                if (assigneeAllowed && OwnerOf(t) != c.AssigneeId) continue;
                result.Add(t);
            }
            return result;
        }

        // ── Time helpers ───────────────────────────────────────────────────

        private static bool Within(DateTime value, DateTime a, DateTime b)
        {
            return value >= a && value <= b;
        }

        private static bool Within(DateTime? value, DateTime a, DateTime b)
        {
            return value.HasValue && value.Value >= a && value.Value <= b;
        }

        /// <summary>Open at a moment: created by then and not completed, cancelled or rejected.</summary>
        private static bool IsOpenAt(DashboardTaskRow t, DateTime at)
        {
            if (t.CreatedAt > at) return false;
            if (t.Status == "Cancelled" || t.Status == "Rejected") return false;
            if (t.CompletedAt.HasValue && t.CompletedAt.Value <= at) return false;
            return true;
        }

        private static bool IsOverdueAt(DashboardTaskRow t, DateTime at)
        {
            return IsOpenAt(t, at) && t.DueDate.HasValue && t.DueDate.Value < at;
        }

        private static DateTime KeyOf(DateTime t, bool weekly)
        {
            DateTime day = t.Date;
            if (!weekly) return day;
            int back = ((int)day.DayOfWeek + 6) % 7; // Monday = 0
            return day.AddDays(-back);
        }

        private static double Round(double value, int digits)
        {
            return Math.Round(value, digits, MidpointRounding.AwayFromZero);
        }

        private static string Stamp(DateTime value)
        {
            return value.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        }

        // ── Period measures ────────────────────────────────────────────────

        private sealed class Measure
        {
            public int Created;
            public int Completed;
            public int Open;
            public int Overdue;
            public double? OnTimePct;
            public double? AvgHours;
        }

        private static Measure MeasurePeriod(List<DashboardTaskRow> tasks, DateTime a, DateTime b)
        {
            var completed = tasks.Where(t => Within(t.CompletedAt, a, b)).ToList();
            bool anyDue = completed.Any(t => t.DueDate.HasValue);
            int onTime = completed.Count(t => t.DueDate.HasValue && t.CompletedAt!.Value <= t.DueDate.Value);

            var m = new Measure();
            m.Created = tasks.Count(t => Within(t.CreatedAt, a, b));
            m.Completed = completed.Count;
            m.Open = tasks.Count(t => IsOpenAt(t, b));
            m.Overdue = tasks.Count(t => IsOverdueAt(t, b));
            if (completed.Count > 0 && anyDue)
                m.OnTimePct = Round(onTime * 100.0 / completed.Count, 1);
            if (completed.Count > 0)
                m.AvgHours = Round(completed.Average(t => (t.CompletedAt!.Value - t.CreatedAt).TotalHours), 1);
            return m;
        }

        private static KpiValueDto Kpi(double? current, double? previous)
        {
            return new KpiValueDto(current ?? 0, previous);
        }

        // ── The dashboard ──────────────────────────────────────────────────

        /// <summary>
        /// Builds the full response. <paramref name="rows"/> must already be limited to what the user may see
        /// (the repository applies the Employee scope in the query). Sections the permissions do not allow are omitted or empty.
        /// </summary>
        public static DashboardResponseDto Build(
            IReadOnlyList<DashboardTaskRow> rows,
            DashboardLookups lookups,
            DashboardCriteria c,
            bool selfScope,
            List<string> permissions,
            DateTime now)
        {
            List<DashboardTaskRow> tasks = Filter(rows, c, permissions);
            bool weekly = c.Days > DayBucketLimit;

            Measure cur = MeasurePeriod(tasks, c.Start, c.End);
            Measure prev = MeasurePeriod(tasks, c.PreviousStart, c.PreviousEnd);

            var userNames = new Dictionary<int, string>();
            foreach (DashboardUserRow u in lookups.Users) userNames[u.Id] = u.Name;
            Func<int, string> nameOf = id => userNames.ContainsKey(id) ? userNames[id] : "User " + id;

            // Other people's names are only shown with the 'workload' permission (an Employee only sees their own work).
            bool showPeople = permissions.Contains(DashboardPermissions.Workload);

            // Series: every bucket in the range is present, with zeros where nothing happened.
            var bucketStarts = new List<DateTime>();
            var createdPerBucket = new List<int>();
            var completedPerBucket = new List<int>();
            var bucketIndex = new Dictionary<DateTime, int>();
            for (DateTime k = KeyOf(c.Start, weekly); k <= c.End; k = k.AddDays(weekly ? 7 : 1))
            {
                bucketIndex[k] = bucketStarts.Count;
                bucketStarts.Add(k);
                createdPerBucket.Add(0);
                completedPerBucket.Add(0);
            }
            foreach (DashboardTaskRow t in tasks)
            {
                int i;
                if (Within(t.CreatedAt, c.Start, c.End) && bucketIndex.TryGetValue(KeyOf(t.CreatedAt, weekly), out i))
                    createdPerBucket[i]++;
                if (t.CompletedAt.HasValue && Within(t.CompletedAt.Value, c.Start, c.End) && bucketIndex.TryGetValue(KeyOf(t.CompletedAt.Value, weekly), out i))
                    completedPerBucket[i]++;
            }
            var series = new List<TrendPointDto>();
            for (int i = 0; i < bucketStarts.Count; i++)
                series.Add(new TrendPointDto(bucketStarts[i].ToString(DateFormat, CultureInfo.InvariantCulture), createdPerBucket[i], completedPerBucket[i]));

            // Breakdowns cover tasks created in the range.
            List<DashboardTaskRow> created = tasks.Where(t => Within(t.CreatedAt, c.Start, c.End)).ToList();

            var byStatus = Statuses.Select(s => new StatusCountDto(s, created.Count(t => t.Status == s))).ToList();
            var byPriority = Priorities.Select(p => new PriorityCountDto(p, created.Count(t => t.Priority == p))).ToList();

            var categoryTotals = new Dictionary<int, CategoryCountDto>();
            foreach (DashboardTaskRow t in created)
            {
                int key = t.CategoryId ?? -1;
                CategoryCountDto existing;
                if (categoryTotals.TryGetValue(key, out existing))
                {
                    categoryTotals[key] = existing with { Count = existing.Count + 1 };
                }
                else
                {
                    DashboardCategoryRow? known = lookups.Categories.FirstOrDefault(x => x.Id == t.CategoryId);
                    string name = known != null ? known.Name : (t.CategoryName ?? "Uncategorized");
                    string color = known != null ? known.ColorHex : (t.CategoryColor ?? "#9ca3af");
                    categoryTotals[key] = new CategoryCountDto(t.CategoryId, name, color, 1);
                }
            }
            var byCategory = categoryTotals.Values
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var workflowTotals = new Dictionary<int, WorkflowAccumulator>();
            foreach (DashboardTaskRow t in created)
            {
                WorkflowAccumulator acc;
                if (!workflowTotals.TryGetValue(t.WorkflowId, out acc))
                {
                    DashboardWorkflowRow? known = lookups.Workflows.FirstOrDefault(x => x.Id == t.WorkflowId);
                    acc = new WorkflowAccumulator();
                    acc.WorkflowId = t.WorkflowId;
                    acc.Title = known != null ? known.Title : (t.WorkflowTitle ?? ("Workflow " + t.WorkflowId));
                    workflowTotals[t.WorkflowId] = acc;
                }
                acc.Total++;
                if (t.CompletedAt.HasValue)
                {
                    acc.Completed++;
                    acc.Hours.Add((t.CompletedAt.Value - t.CreatedAt).TotalHours);
                }
            }
            var byWorkflow = workflowTotals.Values
                .Select(a => new WorkflowStatDto(a.WorkflowId, a.Title, a.Total, a.Completed, a.Hours.Count > 0 ? Round(a.Hours.Average(), 1) : (double?)null))
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Workload: only with the permission, never in the self scope.
            var workload = new List<WorkloadRowDto>();
            if (!selfScope && permissions.Contains(DashboardPermissions.Workload))
            {
                var perPerson = new Dictionary<int, int[]>(); // [pending, in progress, completed]
                foreach (DashboardTaskRow t in created)
                {
                    int? owner = OwnerOf(t);
                    if (!owner.HasValue) continue;
                    int[] counts;
                    if (!perPerson.TryGetValue(owner.Value, out counts))
                    {
                        counts = new int[3];
                        perPerson[owner.Value] = counts;
                    }
                    if (t.Status == "Pending") counts[0]++;
                    else if (t.Status == "In Progress") counts[1]++;
                    else if (t.Status == "Completed") counts[2]++;
                }
                workload = perPerson
                    .Select(p => new WorkloadRowDto(p.Key, nameOf(p.Key), p.Value[0], p.Value[1], p.Value[2]))
                    .OrderByDescending(r => r.Pending + r.InProgress + r.Completed)
                    .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(TopCount)
                    .ToList();
            }

            // Overdue at the end of the range. Days past due are rounded up: part of a day counts as 1.
            var overdue = tasks
                .Where(t => IsOverdueAt(t, c.End))
                .Select(t =>
                {
                    int? owner = OwnerOf(t);
                    return new OverdueTaskDto(
                        t.TaskId,
                        t.Title,
                        showPeople && owner.HasValue ? nameOf(owner.Value) : null,
                        t.WorkflowTitle,
                        t.Priority,
                        Stamp(t.DueDate!.Value),
                        (int)Math.Ceiling((c.End - t.DueDate.Value).TotalDays));
                })
                .OrderByDescending(t => t.DaysOverdue)
                .ThenBy(t => t.TaskId)
                .ToList();

            var aging = new int[4];
            foreach (OverdueTaskDto t in overdue)
            {
                int bucket = t.DaysOverdue <= 3 ? 0 : t.DaysOverdue <= 7 ? 1 : t.DaysOverdue <= 14 ? 2 : 3;
                aging[bucket]++;
            }
            var overdueAging = new List<AgingBucketDto>();
            for (int i = 0; i < AgingLabels.Length; i++)
                overdueAging.Add(new AgingBucketDto(AgingLabels[i], aging[i]));

            var options = new DashboardOptionsDto(
                lookups.Workflows.Select(w => new WorkflowOptionDto(w.Id, w.Title)).ToList(),
                lookups.Categories.Select(x => new CategoryOptionDto(x.Id, x.Name, x.ColorHex)).ToList(),
                permissions.Contains(DashboardPermissions.AssigneeFilter)
                    ? lookups.Users.Where(u => !u.IsDeleted).OrderBy(u => u.Name, StringComparer.OrdinalIgnoreCase).Select(u => new AssigneeOptionDto(u.Id, u.Name)).ToList()
                    : new List<AssigneeOptionDto>());

            OrgTotalsDto? totals = null;
            if (permissions.Contains(DashboardPermissions.OrgTotals))
            {
                totals = new OrgTotalsDto(
                    lookups.Users.Count(u => !u.IsDeleted),
                    lookups.Workflows.Count,
                    lookups.Workflows.Count(w => w.Status == "Active"));
            }

            return new DashboardResponseDto(
                selfScope ? "self" : "all",
                now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                permissions,
                totals,
                new DashboardRangeDto(
                    c.From,
                    c.To,
                    c.PreviousStart.ToString(DateFormat, CultureInfo.InvariantCulture),
                    c.PreviousEnd.ToString(DateFormat, CultureInfo.InvariantCulture),
                    weekly ? "week" : "day"),
                new DashboardKpisDto(
                    Kpi(cur.Created, prev.Created),
                    Kpi(cur.Completed, prev.Completed),
                    Kpi(cur.Open, prev.Open),
                    Kpi(cur.Overdue, prev.Overdue),
                    Kpi(cur.OnTimePct, prev.OnTimePct),
                    Kpi(cur.AvgHours, prev.AvgHours)),
                series,
                byStatus,
                byPriority,
                byCategory,
                byWorkflow,
                workload,
                overdueAging,
                overdue.Take(TopCount).ToList(),
                options);
        }

        private sealed class WorkflowAccumulator
        {
            public int WorkflowId;
            public string Title = string.Empty;
            public int Total;
            public int Completed;
            public List<double> Hours = new List<double>();
        }

        // ── CSV export ─────────────────────────────────────────────────────

        /// <summary>
        /// CSV of the filtered tasks created in the range. Overdue is evaluated at the end of the range.
        /// Text cells that start with = + - @ are prefixed with an apostrophe so a spreadsheet cannot run them as formulas.
        /// </summary>
        public static DashboardCsvResult BuildTasksCsv(
            IReadOnlyList<DashboardTaskRow> rows,
            DashboardLookups lookups,
            DashboardCriteria c,
            IReadOnlyCollection<string> permissions)
        {
            var userNames = new Dictionary<int, string>();
            foreach (DashboardUserRow u in lookups.Users) userNames[u.Id] = u.Name;
            bool showPeople = permissions.Contains(DashboardPermissions.Workload);

            List<DashboardTaskRow> selected = Filter(rows, c, permissions)
                .Where(t => Within(t.CreatedAt, c.Start, c.End))
                .OrderBy(t => t.TaskId)
                .ToList();

            var sb = new StringBuilder();
            sb.Append("TaskId,Title,Workflow,Category,Assignee,Status,Priority,CreatedAt,DueDate,CompletedAt,CycleHours,Overdue");
            foreach (DashboardTaskRow t in selected)
            {
                int? owner = OwnerOf(t);
                string assignee = showPeople && owner.HasValue && userNames.ContainsKey(owner.Value) ? userNames[owner.Value] : string.Empty;
                string cycle = t.CompletedAt.HasValue
                    ? Round((t.CompletedAt.Value - t.CreatedAt).TotalHours, 2).ToString("0.##", CultureInfo.InvariantCulture)
                    : string.Empty;

                sb.Append("\r\n");
                sb.Append(t.TaskId.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(Cell(t.Title)).Append(',');
                sb.Append(Cell(t.WorkflowTitle)).Append(',');
                sb.Append(Cell(t.CategoryName)).Append(',');
                sb.Append(Cell(assignee)).Append(',');
                sb.Append(Cell(t.Status)).Append(',');
                sb.Append(Cell(t.Priority)).Append(',');
                sb.Append(Stamp(t.CreatedAt)).Append(',');
                sb.Append(t.DueDate.HasValue ? Stamp(t.DueDate.Value) : string.Empty).Append(',');
                sb.Append(t.CompletedAt.HasValue ? Stamp(t.CompletedAt.Value) : string.Empty).Append(',');
                sb.Append(cycle).Append(',');
                sb.Append(IsOverdueAt(t, c.End) ? "Yes" : "No");
            }

            string fileName = "tasks-" + c.From + "_" + c.To + ".csv";
            return new DashboardCsvResult(sb.ToString(), fileName, selected.Count);
        }

        private static string Cell(string? value)
        {
            string s = value ?? string.Empty;
            if (s.Length > 0)
            {
                char first = s[0];
                if (first == '=' || first == '+' || first == '-' || first == '@' || first == '\t' || first == '\r')
                    s = "'" + s;
            }
            if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
