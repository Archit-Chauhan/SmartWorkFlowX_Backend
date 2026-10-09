using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;

namespace SmartWorkFlowX.Tests.Services
{
    /// <summary>
    /// Expected numbers are ported from the frontend reference suite (src/test/demoDashboard.test.ts).
    /// Range 2025-03-10..2025-03-16 (Mon-Sun), previous period 2025-03-03..2025-03-09, "now" 2025-03-20.
    /// </summary>
    public class DashboardCalculatorTests
    {
        private static readonly DateTime Now = Utc(2025, 3, 20);
        private const string CsvHeader = "TaskId,Title,Workflow,Category,Assignee,Status,Priority,CreatedAt,DueDate,CompletedAt,CycleHours,Overdue";

        // ── Builders ───────────────────────────────────────────────────────

        private static DateTime Utc(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        {
            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }

        private static DashboardTaskRow Mk(
            int id,
            DateTime? createdAt = null,
            DateTime? due = null,
            DateTime? completed = null,
            string status = "In Progress",
            string priority = "Medium",
            int workflowId = 1,
            string? workflowTitle = "Expense Approval",
            int? categoryId = 1,
            string? categoryName = "Finance",
            int? assignedTo = null,
            int? originalAssignedTo = null,
            string? title = null)
        {
            return new DashboardTaskRow
            {
                TaskId = id,
                Title = title ?? ("Task " + id),
                WorkflowId = workflowId,
                WorkflowTitle = workflowTitle,
                AssignedTo = assignedTo,
                OriginalAssignedTo = originalAssignedTo,
                Status = status,
                Priority = priority,
                CategoryId = categoryId,
                CategoryName = categoryName,
                CategoryColor = categoryId == null ? null : (categoryId == 2 ? "#8a3ffc" : "#0f62fe"),
                CreatedAt = createdAt ?? Utc(2025, 3, 10, 10),
                DueDate = due,
                CompletedAt = completed
            };
        }

        private static List<DashboardTaskRow> Tasks()
        {
            return new List<DashboardTaskRow>
            {
                Mk(1, Utc(2025, 3, 10, 10), Utc(2025, 3, 12), Utc(2025, 3, 11, 10), status: "Completed", priority: "High", assignedTo: 4),
                Mk(2, Utc(2025, 3, 5, 12), Utc(2025, 3, 8), Utc(2025, 3, 12, 12), status: "Completed", workflowId: 2, workflowTitle: "Leave Request", categoryId: 2, categoryName: "HR"),
                Mk(3, Utc(2025, 3, 4, 9), Utc(2025, 3, 7), Utc(2025, 3, 6, 9), status: "Completed"),
                Mk(4, Utc(2025, 3, 12, 8), Utc(2025, 3, 14), priority: "Low", assignedTo: 5),
                Mk(5, Utc(2025, 3, 1, 8), Utc(2025, 3, 8), status: "Pending", assignedTo: 4),
                Mk(6, Utc(2025, 3, 11, 8), Utc(2025, 3, 20), status: "Cancelled"),
                Mk(7, Utc(2025, 3, 20, 8), Utc(2025, 3, 25))
            };
        }

        private static DashboardLookups Lookups()
        {
            return new DashboardLookups
            {
                Users = new List<DashboardUserRow>
                {
                    new DashboardUserRow { Id = 4, Name = "Dan Patel", IsDeleted = false },
                    new DashboardUserRow { Id = 5, Name = "Eve Torres", IsDeleted = false },
                    new DashboardUserRow { Id = 7, Name = "Grace Kim", IsDeleted = true }
                },
                Workflows = new List<DashboardWorkflowRow>
                {
                    new DashboardWorkflowRow { Id = 1, Title = "Expense Approval", Status = "Active" },
                    new DashboardWorkflowRow { Id = 2, Title = "Leave Request", Status = "Draft" }
                },
                Categories = new List<DashboardCategoryRow>
                {
                    new DashboardCategoryRow { Id = 1, Name = "Finance", ColorHex = "#0f62fe" },
                    new DashboardCategoryRow { Id = 2, Name = "HR", ColorHex = "#8a3ffc" }
                }
            };
        }

        private static DashboardRequest March()
        {
            return new DashboardRequest { From = "2025-03-10", To = "2025-03-16" };
        }

        private static DashboardRequest Range(string from, string to)
        {
            return new DashboardRequest { From = from, To = to };
        }

        private static DashboardResponseDto Run(
            DashboardRequest? request = null,
            List<DashboardTaskRow>? rows = null,
            string? role = "Admin",
            bool selfScope = false)
        {
            DashboardCriteria criteria = DashboardCalculator.ParseRequest(request ?? March(), Now);
            return DashboardCalculator.Build(
                rows ?? Tasks(), Lookups(), criteria, selfScope, DashboardPolicy.PermissionsForRole(role), Now);
        }

        private static DashboardCsvResult Csv(
            DashboardRequest? request = null,
            List<DashboardTaskRow>? rows = null,
            string? role = "Admin")
        {
            DashboardCriteria criteria = DashboardCalculator.ParseRequest(request ?? March(), Now);
            return DashboardCalculator.BuildTasksCsv(
                rows ?? Tasks(), Lookups(), criteria, DashboardPolicy.PermissionsForRole(role));
        }

        private static string[] CsvLines(DashboardCsvResult csv)
        {
            return csv.Content.Split("\r\n");
        }

        // ── KPIs ───────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D01: Range echoes the dates, the previous period of equal length, the day bucket, scope and generation time")]
        public void Build_ReportsRangeAndPreviousPeriod()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(new DashboardRangeDto("2025-03-10", "2025-03-16", "2025-03-03", "2025-03-09", "day"), r.Range);
            Assert.Equal("all", r.Scope);
            Assert.Equal("2025-03-20T00:00:00.000Z", r.GeneratedAt);
        }

        [Fact(DisplayName = "TC-D02: Created and completed count only events inside the range (previous period alongside)")]
        public void Build_CountsCreatedAndCompletedInsideRange()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(new KpiValueDto(3, 2), r.Kpis.Created);
            Assert.Equal(new KpiValueDto(2, 1), r.Kpis.Completed);
        }

        [Fact(DisplayName = "TC-D03: Open and overdue are evaluated at the end of each period")]
        public void Build_CountsOpenAndOverdueAtPeriodEnd()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(new KpiValueDto(2, 2), r.Kpis.Open);
            Assert.Equal(new KpiValueDto(2, 2), r.Kpis.Overdue);
        }

        [Fact(DisplayName = "TC-D04: On-time rate and average completion hours for current and previous period")]
        public void Build_ComputesOnTimeRateAndAverageHours()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(new KpiValueDto(50, 100), r.Kpis.OnTimeRatePct);
            Assert.Equal(new KpiValueDto(96, 48), r.Kpis.AvgCompletionHours);
        }

        [Fact(DisplayName = "TC-D05: Previous on-time rate and average hours are null when nothing was completed")]
        public void Build_PreviousIsNullWhenNothingCompleted()
        {
            DashboardResponseDto r = Run(Range("2025-06-01", "2025-06-07"));

            Assert.Null(r.Kpis.OnTimeRatePct.Previous);
            Assert.Null(r.Kpis.AvgCompletionHours.Previous);
            Assert.Equal(new KpiValueDto(0, 0), r.Kpis.Completed);
        }

        [Fact(DisplayName = "TC-D06: The last tick of the last day is inside the range, the next midnight is outside")]
        public void Build_RangeEndIsInclusiveToTheTick()
        {
            DateTime lastTick = Utc(2025, 3, 16, 23, 59, 59).AddTicks(9999999);
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, lastTick),
                Mk(2, Utc(2025, 3, 17))
            };

            DashboardResponseDto r = Run(rows: rows);

            Assert.Equal(1, r.Kpis.Created.Current);
        }

        // ── Request parsing ────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D07: ParseRequest rejects dates that are not yyyy-MM-dd")]
        public void ParseRequest_BadDateFormat_Throws()
        {
            Assert.Throws<ArgumentException>(() => DashboardCalculator.ParseRequest(Range("2025/03/10", "2025-03-16"), Now));
            Assert.Throws<ArgumentException>(() => DashboardCalculator.ParseRequest(Range("2025-03-10", "16-03-2025"), Now));
            Assert.Throws<ArgumentException>(() => DashboardCalculator.ParseRequest(Range("not a date", "2025-03-16"), Now));
        }

        [Fact(DisplayName = "TC-D08: ParseRequest rejects a 'from' after 'to'")]
        public void ParseRequest_FromAfterTo_Throws()
        {
            Assert.Throws<ArgumentException>(() => DashboardCalculator.ParseRequest(Range("2025-03-17", "2025-03-16"), Now));
        }

        [Fact(DisplayName = "TC-D09: ParseRequest accepts 366 days and rejects 367")]
        public void ParseRequest_RangeLimitIs366Days()
        {
            DashboardCriteria ok = DashboardCalculator.ParseRequest(Range("2024-01-01", "2024-12-31"), Now);

            Assert.Equal(366, ok.Days);
            Assert.Throws<ArgumentException>(() => DashboardCalculator.ParseRequest(Range("2024-01-01", "2025-01-01"), Now));
        }

        [Fact(DisplayName = "TC-D10: Missing dates default to the 30 days ending at today")]
        public void ParseRequest_NoDates_DefaultsToLast30Days()
        {
            DashboardCriteria c = DashboardCalculator.ParseRequest(new DashboardRequest(), Utc(2025, 3, 20, 15, 30));

            Assert.Equal("2025-02-19", c.From);
            Assert.Equal("2025-03-20", c.To);
            Assert.Equal(30, c.Days);
        }

        [Fact(DisplayName = "TC-D11: Only 'to' given means the 30 days ending at 'to'")]
        public void ParseRequest_OnlyTo_Means30DaysEndingAtTo()
        {
            DashboardCriteria c = DashboardCalculator.ParseRequest(new DashboardRequest { To = "2025-03-10" }, Now);

            Assert.Equal("2025-02-09", c.From);
            Assert.Equal("2025-03-10", c.To);
            Assert.Equal(30, c.Days);
        }

        [Fact(DisplayName = "TC-D12: Period bounds use ticks and blank filters are cleaned")]
        public void ParseRequest_BuildsTickBoundsAndCleansFilters()
        {
            var request = March();
            request.Status = "   ";
            request.Priority = " High ";

            DashboardCriteria c = DashboardCalculator.ParseRequest(request, Now);

            Assert.Equal(new DateTime(2025, 3, 10), c.Start);
            Assert.Equal(new DateTime(2025, 3, 17).AddTicks(-1), c.End);
            Assert.Equal(new DateTime(2025, 3, 3), c.PreviousStart);
            Assert.Equal(new DateTime(2025, 3, 10).AddTicks(-1), c.PreviousEnd);
            Assert.Equal(7, c.Days);
            Assert.Null(c.Status);
            Assert.Equal("High", c.Priority);
        }

        // ── Series ─────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D13: Day buckets cover every day of the range with zeros filled in")]
        public void Build_DaySeriesFillsZeros()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(7, r.Series.Count);
            Assert.Equal(new TrendPointDto("2025-03-10", 1, 0), r.Series[0]);
            Assert.Equal(new TrendPointDto("2025-03-11", 1, 1), r.Series[1]);
            Assert.Equal(new TrendPointDto("2025-03-12", 1, 1), r.Series[2]);
            Assert.Equal(new TrendPointDto("2025-03-13", 0, 0), r.Series[3]);
            Assert.Equal("2025-03-16", r.Series[6].Date);
        }

        [Fact(DisplayName = "TC-D14: 45 days stay on day buckets, 46 days switch to Monday-start week buckets")]
        public void Build_SwitchesToWeeksAt46Days()
        {
            DashboardResponseDto d45 = Run(Range("2025-01-01", "2025-02-14"));
            DashboardResponseDto d46 = Run(Range("2025-01-01", "2025-02-15"));

            Assert.Equal("day", d45.Range.Bucket);
            Assert.Equal(45, d45.Series.Count);
            Assert.Equal("week", d46.Range.Bucket);
            Assert.Equal(
                new List<string> { "2024-12-30", "2025-01-06", "2025-01-13", "2025-01-20", "2025-01-27", "2025-02-03", "2025-02-10" },
                d46.Series.Select(p => p.Date).ToList());
        }

        [Fact(DisplayName = "TC-D15: A created event falls into the Monday-start week it belongs to")]
        public void Build_CreatedEventLandsInItsWeek()
        {
            var rows = new List<DashboardTaskRow> { Mk(1, Utc(2025, 1, 8, 10)) };

            DashboardResponseDto r = Run(Range("2025-01-01", "2025-02-15"), rows);

            Assert.Equal(1, r.Series.Single(p => p.Date == "2025-01-06").Created);
            Assert.Equal(1, r.Series.Sum(p => p.Created));
        }

        [Fact(DisplayName = "TC-D16: A completed event falls into the week of its completion, a Sunday belongs to the week before")]
        public void Build_CompletedEventLandsInItsWeek()
        {
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, Utc(2024, 12, 1), completed: Utc(2025, 1, 19, 12), status: "Completed"), // Sunday -> week of Jan 13
                Mk(2, Utc(2024, 12, 1), completed: Utc(2025, 1, 20, 12), status: "Completed")  // Monday -> week of Jan 20
            };

            DashboardResponseDto r = Run(Range("2025-01-01", "2025-02-15"), rows);

            Assert.Equal(1, r.Series.Single(p => p.Date == "2025-01-13").Completed);
            Assert.Equal(1, r.Series.Single(p => p.Date == "2025-01-20").Completed);
            Assert.Equal(2, r.Series.Sum(p => p.Completed));
        }

        // ── Breakdowns ─────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D17: Status and priority breakdowns list every value and count tasks created in the range")]
        public void Build_StatusAndPriorityBreakdowns()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(
                new List<string> { "Pending", "In Progress", "Completed", "Rejected", "Cancelled" },
                r.ByStatus.Select(s => s.Status).ToList());
            Assert.Equal(1, r.ByStatus.Single(s => s.Status == "In Progress").Count);
            Assert.Equal(1, r.ByStatus.Single(s => s.Status == "Completed").Count);
            Assert.Equal(1, r.ByStatus.Single(s => s.Status == "Cancelled").Count);
            Assert.Equal(0, r.ByStatus.Single(s => s.Status == "Pending").Count);
            Assert.Equal(
                new List<PriorityCountDto> { new PriorityCountDto("High", 1), new PriorityCountDto("Medium", 1), new PriorityCountDto("Low", 1) },
                r.ByPriority);
        }

        [Fact(DisplayName = "TC-D18: Category and workflow breakdowns count tasks created in the range, with average cycle hours")]
        public void Build_CategoryAndWorkflowBreakdowns()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(
                new List<CategoryCountDto> { new CategoryCountDto(1, "Finance", "#0f62fe", 3) },
                r.ByCategory);
            Assert.Equal(
                new List<WorkflowStatDto> { new WorkflowStatDto(1, "Expense Approval", 3, 1, 24) },
                r.ByWorkflow);
        }

        [Fact(DisplayName = "TC-D19: Categories are ordered by count then name; lookup names win; no category means Uncategorized")]
        public void Build_CategoryOrderingAndFallbacks()
        {
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, categoryId: 1, categoryName: "Old finance name"),
                Mk(2, categoryId: 2, categoryName: "HR"),
                Mk(3, categoryId: 2, categoryName: "HR"),
                Mk(4, categoryId: null, categoryName: null)
            };

            DashboardResponseDto r = Run(rows: rows);

            Assert.Equal(3, r.ByCategory.Count);
            Assert.Equal(new CategoryCountDto(2, "HR", "#8a3ffc", 2), r.ByCategory[0]);
            Assert.Equal(new CategoryCountDto(1, "Finance", "#0f62fe", 1), r.ByCategory[1]);
            Assert.Equal(new CategoryCountDto(null, "Uncategorized", "#9ca3af", 1), r.ByCategory[2]);
        }

        [Fact(DisplayName = "TC-D20: A workflow with no completed task has a null average cycle time")]
        public void Build_WorkflowWithoutCompletionsHasNullAverage()
        {
            var rows = new List<DashboardTaskRow> { Mk(4, Utc(2025, 3, 12, 8)) };

            DashboardResponseDto r = Run(rows: rows);

            WorkflowStatDto wf = Assert.Single(r.ByWorkflow);
            Assert.Equal(1, wf.Total);
            Assert.Equal(0, wf.Completed);
            Assert.Null(wf.AvgCycleHours);
        }

        [Fact(DisplayName = "TC-D21: Overdue aging always returns all four buckets with the right boundaries")]
        public void Build_OverdueAgingUsesAllFourBuckets()
        {
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, due: Utc(2025, 3, 14)), // 2.99 days -> 3 -> 1-3
                Mk(2, due: Utc(2025, 3, 13)), // 3.99 days -> 4 -> 4-7
                Mk(3, due: Utc(2025, 3, 9)),  // 7.99 days -> 8 -> 8-14
                Mk(4, due: Utc(2025, 3, 1))   // 15.99 days -> 16 -> 15+
            };

            DashboardResponseDto r = Run(rows: rows);

            Assert.Equal(
                new List<AgingBucketDto>
                {
                    new AgingBucketDto("1-3 days", 1),
                    new AgingBucketDto("4-7 days", 1),
                    new AgingBucketDto("8-14 days", 1),
                    new AgingBucketDto("15+ days", 1)
                },
                r.OverdueAging);
        }

        [Fact(DisplayName = "TC-D22: Overdue aging for the sample data and for a range with nothing overdue")]
        public void Build_OverdueAgingSampleAndEmpty()
        {
            DashboardResponseDto r = Run();
            DashboardResponseDto empty = Run(Range("2025-02-01", "2025-02-02"));

            Assert.Equal(new List<int> { 1, 0, 1, 0 }, r.OverdueAging.Select(a => a.Count).ToList());
            Assert.Equal(new List<int> { 0, 0, 0, 0 }, empty.OverdueAging.Select(a => a.Count).ToList());
        }

        [Fact(DisplayName = "TC-D23: Top overdue lists the most overdue task first with assignee and due date")]
        public void Build_TopOverdueMostOverdueFirst()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(new List<(int, int)> { (5, 9), (4, 3) }, r.TopOverdue.Select(t => (t.TaskId, t.DaysOverdue)).ToList());
            OverdueTaskDto first = r.TopOverdue[0];
            Assert.Equal("Dan Patel", first.AssigneeName);
            Assert.Equal("Expense Approval", first.WorkflowTitle);
            Assert.Equal("Medium", first.Priority);
            Assert.Equal("2025-03-08T00:00:00Z", first.DueDate);
        }

        [Fact(DisplayName = "TC-D24: Top overdue is capped at 10 (ties by task id) while aging counts every overdue task")]
        public void Build_TopOverdueCappedAtTen()
        {
            var rows = new List<DashboardTaskRow>();
            for (int id = 112; id >= 101; id--)
                rows.Add(Mk(id, due: Utc(2025, 3, 14)));

            DashboardResponseDto r = Run(rows: rows);

            Assert.Equal(10, r.TopOverdue.Count);
            Assert.Equal(Enumerable.Range(101, 10).ToList(), r.TopOverdue.Select(t => t.TaskId).ToList());
            Assert.Equal(12, r.OverdueAging[0].Count);
        }

        [Fact(DisplayName = "TC-D25: Workload counts tasks created in the range per owner, with names")]
        public void Build_WorkloadPerAssignee()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(
                new List<WorkloadRowDto>
                {
                    new WorkloadRowDto(4, "Dan Patel", 0, 0, 1),
                    new WorkloadRowDto(5, "Eve Torres", 0, 1, 0)
                },
                r.Workload);
        }

        [Fact(DisplayName = "TC-D26: Workload owner is AssignedTo, falling back to OriginalAssignedTo; unowned tasks are skipped")]
        public void Build_WorkloadOwnerFallsBackToOriginalAssignee()
        {
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, status: "Pending", assignedTo: null, originalAssignedTo: 4),
                Mk(2, status: "In Progress", assignedTo: 5, originalAssignedTo: 4),
                Mk(3, status: "Pending")
            };

            DashboardResponseDto r = Run(rows: rows);

            Assert.Equal(
                new List<WorkloadRowDto>
                {
                    new WorkloadRowDto(4, "Dan Patel", 1, 0, 0),
                    new WorkloadRowDto(5, "Eve Torres", 0, 1, 0)
                },
                r.Workload);
        }

        [Fact(DisplayName = "TC-D27: Options list all workflows and categories and the non-deleted users as assignees")]
        public void Build_Options()
        {
            DashboardResponseDto r = Run();

            Assert.Equal(new List<int> { 1, 2 }, r.Options.Workflows.Select(w => w.Id).ToList());
            Assert.Equal(new List<int> { 1, 2 }, r.Options.Categories.Select(c => c.Id).ToList());
            Assert.Equal(new List<int> { 4, 5 }, r.Options.Assignees.Select(a => a.Id).ToList());
            Assert.Equal("Expense Approval", r.Options.Workflows[0].Title);
            Assert.Equal("#8a3ffc", r.Options.Categories[1].ColorHex);
        }

        // ── Filters ────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D28: Priority and status filters narrow the numbers")]
        public void Build_PriorityAndStatusFilters()
        {
            var high = March();
            high.Priority = "High";
            var cancelled = March();
            cancelled.Status = "Cancelled";

            Assert.Equal(1, Run(high).Kpis.Created.Current);
            Assert.Equal(1, Run(cancelled).Kpis.Created.Current);
        }

        [Fact(DisplayName = "TC-D29: Category and workflow filters narrow the numbers")]
        public void Build_CategoryAndWorkflowFilters()
        {
            var hr = March();
            hr.CategoryId = 2;
            var leave = March();
            leave.WorkflowId = 2;

            DashboardResponseDto byCategory = Run(hr);

            Assert.Equal(0, byCategory.Kpis.Created.Current);
            Assert.Equal(1, byCategory.Kpis.Completed.Current);
            Assert.Equal(1, Run(leave).Kpis.Completed.Current);
        }

        [Fact(DisplayName = "TC-D30: The assignee filter applies when the role has the assignee-filter permission")]
        public void Build_AssigneeFilterWithPermission()
        {
            var eve = March();
            eve.AssigneeId = 5;

            DashboardResponseDto r = Run(eve, role: "Manager");

            Assert.Equal(1, r.Kpis.Created.Current);
            Assert.Equal(1, r.Kpis.Overdue.Current);
        }

        [Fact(DisplayName = "TC-D31: The assignee filter is ignored without the permission")]
        public void Build_AssigneeFilterIgnoredWithoutPermission()
        {
            var eve = March();
            eve.AssigneeId = 5;

            DashboardResponseDto r = Run(eve, role: "Contractor");

            Assert.Equal(Run().Kpis.Created.Current, r.Kpis.Created.Current);
            Assert.Empty(r.Options.Assignees);
        }

        // ── Permissions ────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D32: Admin gets workload, assignee options and org totals counting non-deleted users and Active workflows")]
        public void Build_AdminSeesEverything()
        {
            DashboardResponseDto r = Run(role: "Admin");

            Assert.Contains("activity", r.Permissions);
            Assert.Contains("org-totals", r.Permissions);
            Assert.NotEmpty(r.Workload);
            Assert.Equal(2, r.Options.Assignees.Count);
            Assert.NotNull(r.Totals);
            Assert.Equal(new OrgTotalsDto(2, 2, 1), r.Totals);
        }

        [Fact(DisplayName = "TC-D33: Manager gets totals and workload but not audit activity")]
        public void Build_ManagerSeesTotalsButNotActivity()
        {
            DashboardResponseDto r = Run(role: "Manager");

            Assert.Contains("org-totals", r.Permissions);
            Assert.DoesNotContain("activity", r.Permissions);
            Assert.Equal(new OrgTotalsDto(2, 2, 1), r.Totals);
            Assert.NotEmpty(r.Workload);
            Assert.Equal(2, r.Options.Assignees.Count);
        }

        [Fact(DisplayName = "TC-D34: Auditor gets workload and assignee options on all tasks but no totals")]
        public void Build_AuditorHasNoTotals()
        {
            DashboardResponseDto r = Run(role: "Auditor");

            Assert.Equal("all", r.Scope);
            Assert.Contains("workload", r.Permissions);
            Assert.DoesNotContain("org-totals", r.Permissions);
            Assert.Null(r.Totals);
            Assert.NotEmpty(r.Workload);
            Assert.Equal(2, r.Options.Assignees.Count);
        }

        [Fact(DisplayName = "TC-D35: Employee self scope has scope 'self', no workload, no assignees, no totals; the assignee filter is ignored")]
        public void Build_EmployeeSelfScope()
        {
            // The repository has already limited the rows to what employee 4 may see: tasks 1, 2 (acted on) and 5.
            var scopedRows = Tasks().Where(t => t.TaskId == 1 || t.TaskId == 2 || t.TaskId == 5).ToList();
            var eve = March();
            eve.AssigneeId = 5;

            DashboardResponseDto r = Run(eve, scopedRows, role: "Employee", selfScope: true);

            Assert.Equal("self", r.Scope);
            Assert.Empty(r.Workload);
            Assert.Empty(r.Options.Assignees);
            Assert.Null(r.Totals);
            Assert.Equal(1, r.Kpis.Created.Current);
            Assert.Equal(2, r.Kpis.Completed.Current);
            Assert.Equal(1, r.Kpis.Overdue.Current);
        }

        [Fact(DisplayName = "TC-D36: A role without the workload permission gets an empty workload even outside self scope")]
        public void Build_NoWorkloadPermission_NoWorkload()
        {
            DashboardResponseDto r = Run(role: "Employee", selfScope: false);

            Assert.Equal("all", r.Scope);
            Assert.Empty(r.Workload);
            Assert.Null(r.Totals);
        }

        [Fact(DisplayName = "TC-D37: Self scope hides workload even when the permission is present")]
        public void Build_SelfScopeHidesWorkloadEvenForAdminPermissions()
        {
            DashboardResponseDto r = Run(role: "Admin", selfScope: true);

            Assert.Equal("self", r.Scope);
            Assert.Empty(r.Workload);
        }

        // ── CSV export ─────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D38: CSV has the header and one row per task created in the range, ordered by task id")]
        public void BuildTasksCsv_HeaderAndRows()
        {
            string[] lines = CsvLines(Csv());

            Assert.Equal(4, lines.Length);
            Assert.Equal(CsvHeader, lines[0]);
            Assert.Equal("1,Task 1,Expense Approval,Finance,Dan Patel,Completed,High,2025-03-10T10:00:00Z,2025-03-12T00:00:00Z,2025-03-11T10:00:00Z,24,No", lines[1]);
            Assert.Equal("4,Task 4,Expense Approval,Finance,Eve Torres,In Progress,Low,2025-03-12T08:00:00Z,2025-03-14T00:00:00Z,,,Yes", lines[2]);
            Assert.Equal("6,Task 6,Expense Approval,Finance,,Cancelled,Medium,2025-03-11T08:00:00Z,2025-03-20T00:00:00Z,,,No", lines[3]);
        }

        [Fact(DisplayName = "TC-D39: CSV includes only tasks created inside the range, to the tick")]
        public void BuildTasksCsv_OnlyTasksCreatedInRange()
        {
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, Utc(2025, 3, 9, 23, 59, 59).AddTicks(9999999)),
                Mk(2, Utc(2025, 3, 10)),
                Mk(3, Utc(2025, 3, 16, 23, 59, 59).AddTicks(9999999)),
                Mk(4, Utc(2025, 3, 17))
            };

            DashboardCsvResult csv = Csv(rows: rows);

            Assert.Equal(2, csv.RowCount);
            Assert.Equal(3, CsvLines(csv).Length);
            Assert.StartsWith("2,", CsvLines(csv)[1]);
            Assert.StartsWith("3,", CsvLines(csv)[2]);
        }

        [Fact(DisplayName = "TC-D40: CSV quotes cells with commas, quotes and newlines and doubles embedded quotes")]
        public void BuildTasksCsv_EscapesSpecialCharacters()
        {
            var rows = new List<DashboardTaskRow> { Mk(1, title: "Say \"hi\", ok\nbye") };

            DashboardCsvResult csv = Csv(rows: rows);

            Assert.Contains("\r\n1,\"Say \"\"hi\"\", ok\nbye\",Expense Approval,", csv.Content);
        }

        [Fact(DisplayName = "TC-D41: CSV prefixes text cells starting with = + - @ with an apostrophe")]
        public void BuildTasksCsv_GuardsAgainstFormulaInjection()
        {
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, title: "=SUM(A1)"),
                Mk(2, title: "+1"),
                Mk(3, title: "-2"),
                Mk(4, title: "@cmd"),
                Mk(5, title: "plain=ok")
            };

            string[] lines = CsvLines(Csv(rows: rows));

            Assert.StartsWith("1,'=SUM(A1),", lines[1]);
            Assert.StartsWith("2,'+1,", lines[2]);
            Assert.StartsWith("3,'-2,", lines[3]);
            Assert.StartsWith("4,'@cmd,", lines[4]);
            Assert.StartsWith("5,plain=ok,", lines[5]);
        }

        [Fact(DisplayName = "TC-D42: A formula-like cell that also needs quoting gets the apostrophe inside the quotes")]
        public void BuildTasksCsv_FormulaGuardThenQuoting()
        {
            var rows = new List<DashboardTaskRow> { Mk(1, title: "=A1,B1") };

            string[] lines = CsvLines(Csv(rows: rows));

            Assert.StartsWith("1,\"'=A1,B1\",", lines[1]);
        }

        [Fact(DisplayName = "TC-D43: CSV cycle hours are completed minus created, up to two decimals, blank when not completed")]
        public void BuildTasksCsv_CycleHours()
        {
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, Utc(2025, 3, 10, 10), completed: Utc(2025, 3, 10, 11, 30), status: "Completed"),
                Mk(2, Utc(2025, 3, 10, 10), completed: Utc(2025, 3, 10, 10, 20), status: "Completed"),
                Mk(3, Utc(2025, 3, 10, 10))
            };

            string[] lines = CsvLines(Csv(rows: rows));

            Assert.EndsWith(",1.5,No", lines[1]);
            Assert.EndsWith(",0.33,No", lines[2]);
            Assert.EndsWith(",,No", lines[3]);
        }

        [Fact(DisplayName = "TC-D44: CSV Overdue column is Yes only for open tasks past due at the end of the range")]
        public void BuildTasksCsv_OverdueYesNo()
        {
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, due: Utc(2025, 3, 12)),                                                                  // open, past due
                Mk(2, due: Utc(2025, 3, 12), completed: Utc(2025, 3, 11), status: "Completed"),               // done
                Mk(3, due: Utc(2025, 3, 20)),                                                                  // not yet due
                Mk(4)                                                                                          // no due date
            };

            string[] lines = CsvLines(Csv(rows: rows));

            Assert.EndsWith(",Yes", lines[1]);
            Assert.EndsWith(",No", lines[2]);
            Assert.EndsWith(",No", lines[3]);
            Assert.EndsWith(",No", lines[4]);
        }

        [Fact(DisplayName = "TC-D45: CSV file name is tasks-<from>_<to>.csv and RowCount matches the data rows")]
        public void BuildTasksCsv_FileNameAndRowCount()
        {
            DashboardCsvResult csv = Csv();

            Assert.Equal("tasks-2025-03-10_2025-03-16.csv", csv.FileName);
            Assert.Equal(3, csv.RowCount);
        }

        [Fact(DisplayName = "TC-D46: An empty export still has the header, RowCount 0 and no trailing newline")]
        public void BuildTasksCsv_EmptyExport()
        {
            DashboardCsvResult csv = Csv(rows: new List<DashboardTaskRow>());

            Assert.Equal(CsvHeader, csv.Content);
            Assert.Equal(0, csv.RowCount);
        }

        [Fact(DisplayName = "TC-D47: CSV respects the filters, and the assignee filter only with the permission")]
        public void BuildTasksCsv_RespectsFilters()
        {
            var low = March();
            low.Priority = "Low";
            var eve = March();
            eve.AssigneeId = 5;

            Assert.Equal(1, Csv(low).RowCount);
            Assert.Equal(1, Csv(eve, role: "Admin").RowCount);
            Assert.Equal(3, Csv(eve, role: "Contractor").RowCount);
        }

        [Fact(DisplayName = "TC-D48: CSV assignee is the OriginalAssignedTo fallback, blank when the user is unknown")]
        public void BuildTasksCsv_AssigneeColumn()
        {
            var rows = new List<DashboardTaskRow>
            {
                Mk(1, assignedTo: null, originalAssignedTo: 5),
                Mk(2, assignedTo: 99)
            };

            string[] lines = CsvLines(Csv(rows: rows));

            Assert.Contains(",Finance,Eve Torres,In Progress,", lines[1]);
            Assert.Contains(",Finance,,In Progress,", lines[2]);
        }

        // ── Names and filter validation ────────────────────────────────────

        [Fact(DisplayName = "TC-D79: Without the workload permission no other person's name is sent on Most overdue")]
        public void Build_EmployeeGetsNoAssigneeNames()
        {
            DashboardResponseDto r = Run(role: "Employee", selfScope: true);

            Assert.NotEmpty(r.TopOverdue);
            Assert.All(r.TopOverdue, t => Assert.Null(t.AssigneeName));
        }

        [Fact(DisplayName = "TC-D80: Without the workload permission the CSV assignee column is blank")]
        public void BuildTasksCsv_EmployeeAssigneeColumnBlank()
        {
            string[] lines = CsvLines(Csv(role: "Employee"));

            Assert.Equal("1,Task 1,Expense Approval,Finance,,Completed,High,2025-03-10T10:00:00Z,2025-03-12T00:00:00Z,2025-03-11T10:00:00Z,24,No", lines[1]);
        }

        [Fact(DisplayName = "TC-D81: Status and priority filters must be known values")]
        public void ParseRequest_UnknownStatusOrPriority_Throws()
        {
            DashboardRequest badStatus = March();
            badStatus.Status = "In Progess";
            DashboardRequest badPriority = March();
            badPriority.Priority = "Urgent\r\nINJECT";

            Assert.Throws<ArgumentException>(() => DashboardCalculator.ParseRequest(badStatus, Now));
            Assert.Throws<ArgumentException>(() => DashboardCalculator.ParseRequest(badPriority, Now));
        }

        [Fact(DisplayName = "TC-D82: Known status and priority values are accepted and trimmed; an empty value means no filter")]
        public void ParseRequest_KnownValuesAccepted()
        {
            DashboardRequest request = March();
            request.Status = " Completed ";
            request.Priority = "High";
            DashboardCriteria criteria = DashboardCalculator.ParseRequest(request, Now);

            Assert.Equal("Completed", criteria.Status);
            Assert.Equal("High", criteria.Priority);

            DashboardRequest blank = March();
            blank.Status = "  ";
            blank.Priority = "";
            DashboardCriteria none = DashboardCalculator.ParseRequest(blank, Now);

            Assert.Null(none.Status);
            Assert.Null(none.Priority);
        }
    }
}
