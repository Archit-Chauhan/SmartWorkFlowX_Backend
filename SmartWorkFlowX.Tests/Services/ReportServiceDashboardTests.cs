using Moq;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Tests.Services
{
    public class ReportServiceDashboardTests
    {
        private readonly Mock<IReportRepository> _reportRepoMock;
        private readonly Mock<IAuditLogRepository> _auditRepoMock;
        private readonly ReportService _service;

        public ReportServiceDashboardTests()
        {
            _reportRepoMock = new Mock<IReportRepository>();
            _auditRepoMock = new Mock<IAuditLogRepository>();
            _service = new ReportService(_reportRepoMock.Object, _auditRepoMock.Object);

            SetupRows(new List<DashboardTaskRow>());
            _reportRepoMock
                .Setup(r => r.GetDashboardLookupsAsync())
                .ReturnsAsync(new DashboardLookups());
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private void SetupRows(List<DashboardTaskRow> rows)
        {
            _reportRepoMock
                .Setup(r => r.GetDashboardTaskRowsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()))
                .ReturnsAsync(rows);
        }

        private static DashboardRequest March()
        {
            return new DashboardRequest { From = "2025-03-10", To = "2025-03-16" };
        }

        private static DashboardTaskRow Row(int id)
        {
            return new DashboardTaskRow
            {
                TaskId = id,
                Title = "Task " + id,
                WorkflowId = 1,
                WorkflowTitle = "Expense Approval",
                Status = "Pending",
                Priority = "Medium",
                CreatedAt = new DateTime(2025, 3, 11, 9, 0, 0, DateTimeKind.Utc)
            };
        }

        private void VerifyRowsRequestedForUser(int expectedUserId)
        {
            _reportRepoMock.Verify(
                r => r.GetDashboardTaskRowsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.Is<int?>(v => v == expectedUserId)),
                Times.Once);
        }

        private void VerifyRowsRequestedUnscoped()
        {
            _reportRepoMock.Verify(
                r => r.GetDashboardTaskRowsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.Is<int?>(v => v == null)),
                Times.Once);
        }

        // ── Dashboard scope ────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D59: Employee dashboard queries only their own tasks, scope is 'self' and workload is empty")]
        public async Task GetDashboardAsync_Employee_IsScopedToTheUser()
        {
            DashboardResponseDto result = await _service.GetDashboardAsync(March(), 4, "Employee");

            VerifyRowsRequestedForUser(4);
            Assert.Equal("self", result.Scope);
            Assert.Empty(result.Workload);
            Assert.Null(result.Totals);
        }

        [Fact(DisplayName = "TC-D60: Manager dashboard is not scoped to a user")]
        public async Task GetDashboardAsync_Manager_IsUnscoped()
        {
            DashboardResponseDto result = await _service.GetDashboardAsync(March(), 9, "Manager");

            VerifyRowsRequestedUnscoped();
            Assert.Equal("all", result.Scope);
        }

        [Fact(DisplayName = "TC-D61: Admin dashboard is not scoped to a user")]
        public async Task GetDashboardAsync_Admin_IsUnscoped()
        {
            DashboardResponseDto result = await _service.GetDashboardAsync(March(), 1, "Admin");

            VerifyRowsRequestedUnscoped();
            Assert.Equal("all", result.Scope);
            Assert.NotNull(result.Totals);
        }

        [Fact(DisplayName = "TC-D62: Auditor dashboard is not scoped to a user")]
        public async Task GetDashboardAsync_Auditor_IsUnscoped()
        {
            DashboardResponseDto result = await _service.GetDashboardAsync(March(), 3, "Auditor");

            VerifyRowsRequestedUnscoped();
            Assert.Equal("all", result.Scope);
            Assert.Null(result.Totals);
        }

        [Fact(DisplayName = "TC-D63: An unknown role is scoped to the signed-in user")]
        public async Task GetDashboardAsync_UnknownRole_IsScopedToTheUser()
        {
            DashboardResponseDto result = await _service.GetDashboardAsync(March(), 7, "Contractor");

            VerifyRowsRequestedForUser(7);
            Assert.Equal("self", result.Scope);
        }

        [Fact(DisplayName = "TC-D64: A missing role is scoped to the signed-in user")]
        public async Task GetDashboardAsync_NullRole_IsScopedToTheUser()
        {
            DashboardResponseDto result = await _service.GetDashboardAsync(March(), 8, null);

            VerifyRowsRequestedForUser(8);
            Assert.Equal("self", result.Scope);
        }

        [Fact(DisplayName = "TC-D65: The assignee filter in the query string cannot widen an Employee's scope")]
        public async Task GetDashboardAsync_EmployeeAssigneeFilter_DoesNotChangeScope()
        {
            var request = March();
            request.AssigneeId = 5;

            DashboardResponseDto result = await _service.GetDashboardAsync(request, 4, "Employee");

            VerifyRowsRequestedForUser(4);
            Assert.Empty(result.Options.Assignees);
        }

        [Fact(DisplayName = "TC-D66: The repository is asked for the previous period start up to the end of the range")]
        public async Task GetDashboardAsync_QueriesPreviousStartThroughRangeEnd()
        {
            await _service.GetDashboardAsync(March(), 1, "Admin");

            _reportRepoMock.Verify(
                r => r.GetDashboardTaskRowsAsync(
                    It.Is<DateTime>(d => d == new DateTime(2025, 3, 3)),
                    It.Is<DateTime>(d => d == new DateTime(2025, 3, 17).AddTicks(-1)),
                    It.IsAny<int?>()),
                Times.Once);
        }

        // ── Validation ─────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D67: A bad date format throws ArgumentException before any query")]
        public async Task GetDashboardAsync_BadDate_Throws()
        {
            var request = new DashboardRequest { From = "10/03/2025", To = "2025-03-16" };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetDashboardAsync(request, 1, "Admin"));

            _reportRepoMock.Verify(
                r => r.GetDashboardTaskRowsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int?>()),
                Times.Never);
        }

        [Fact(DisplayName = "TC-D68: A 'from' after 'to' throws ArgumentException")]
        public async Task GetDashboardAsync_FromAfterTo_Throws()
        {
            var request = new DashboardRequest { From = "2025-03-17", To = "2025-03-16" };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetDashboardAsync(request, 1, "Admin"));
        }

        [Fact(DisplayName = "TC-D69: A range over 366 days throws ArgumentException")]
        public async Task GetDashboardAsync_RangeTooLong_Throws()
        {
            var request = new DashboardRequest { From = "2024-01-01", To = "2025-03-01" };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetDashboardAsync(request, 1, "Admin"));
        }

        [Fact(DisplayName = "TC-D70: A request with no dates uses the default 30-day window")]
        public async Task GetDashboardAsync_NoDates_UsesThirtyDays()
        {
            DashboardResponseDto result = await _service.GetDashboardAsync(new DashboardRequest(), 1, "Admin");

            DateTime from = DateTime.ParseExact(result.Range.From, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            DateTime to = DateTime.ParseExact(result.Range.To, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(29, (to - from).TotalDays);
            Assert.Equal("day", result.Range.Bucket);
        }

        // ── Export ─────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-D71: Export returns the CSV with the row count and file name")]
        public async Task ExportDashboardTasksAsync_ReturnsCsv()
        {
            SetupRows(new List<DashboardTaskRow> { Row(1), Row(2) });

            DashboardCsvResult csv = await _service.ExportDashboardTasksAsync(March(), 1, "Admin");

            Assert.Equal(2, csv.RowCount);
            Assert.Equal("tasks-2025-03-10_2025-03-16.csv", csv.FileName);
            Assert.StartsWith("TaskId,Title,Workflow,Category,Assignee,Status,Priority,CreatedAt,DueDate,CompletedAt,CycleHours,Overdue", csv.Content);
            Assert.Equal(3, csv.Content.Split("\r\n").Length);
        }

        [Fact(DisplayName = "TC-D72: Export writes exactly one audit log entry through AddAsync then SaveAsync")]
        public async Task ExportDashboardTasksAsync_WritesOneAuditLog()
        {
            SetupRows(new List<DashboardTaskRow> { Row(1), Row(2), Row(3) });
            var calls = new List<string>();
            AuditLog? captured = null;
            _auditRepoMock
                .Setup(a => a.AddAsync(It.IsAny<AuditLog>()))
                .Callback<AuditLog>(log => { calls.Add("add"); captured = log; })
                .Returns(Task.CompletedTask);
            _auditRepoMock
                .Setup(a => a.SaveAsync())
                .Callback(() => calls.Add("save"))
                .Returns(Task.CompletedTask);

            await _service.ExportDashboardTasksAsync(March(), 42, "Manager");

            _auditRepoMock.Verify(a => a.AddAsync(It.IsAny<AuditLog>()), Times.Once);
            _auditRepoMock.Verify(a => a.SaveAsync(), Times.Once);
            Assert.Equal(new List<string> { "add", "save" }, calls);
            Assert.NotNull(captured);
            Assert.Equal(42, captured!.UserId);
            Assert.Equal("Reports", captured.EntityName);
            Assert.Contains("3 rows", captured.Action);
            Assert.Contains("2025-03-10", captured.Action);
            Assert.Contains("2025-03-16", captured.Action);
        }

        [Fact(DisplayName = "TC-D73: The audit entry records the filters used for the export")]
        public async Task ExportDashboardTasksAsync_AuditMentionsFilters()
        {
            AuditLog? captured = null;
            _auditRepoMock
                .Setup(a => a.AddAsync(It.IsAny<AuditLog>()))
                .Callback<AuditLog>(log => captured = log)
                .Returns(Task.CompletedTask);
            var request = March();
            request.Priority = "High";
            request.CategoryId = 2;

            await _service.ExportDashboardTasksAsync(request, 1, "Admin");

            Assert.NotNull(captured);
            Assert.Contains("priority=High", captured!.Action);
            Assert.Contains("categoryId=2", captured.Action);
        }

        [Fact(DisplayName = "TC-D74: Export is allowed for every known role")]
        public async Task ExportDashboardTasksAsync_AllKnownRolesAllowed()
        {
            foreach (string role in new[] { "Admin", "Manager", "Auditor", "Employee" })
            {
                DashboardCsvResult csv = await _service.ExportDashboardTasksAsync(March(), 1, role);

                Assert.NotNull(csv);
            }

            _auditRepoMock.Verify(a => a.AddAsync(It.IsAny<AuditLog>()), Times.Exactly(4));
        }

        [Fact(DisplayName = "TC-D75: Export for an Employee is scoped to their own tasks")]
        public async Task ExportDashboardTasksAsync_Employee_IsScopedToTheUser()
        {
            await _service.ExportDashboardTasksAsync(March(), 4, "Employee");

            VerifyRowsRequestedForUser(4);
        }

        [Fact(DisplayName = "TC-D76: Export for Manager, Admin and Auditor is not scoped")]
        public async Task ExportDashboardTasksAsync_PrivilegedRoles_AreUnscoped()
        {
            await _service.ExportDashboardTasksAsync(March(), 1, "Admin");
            await _service.ExportDashboardTasksAsync(March(), 2, "Manager");
            await _service.ExportDashboardTasksAsync(March(), 3, "Auditor");

            _reportRepoMock.Verify(
                r => r.GetDashboardTaskRowsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.Is<int?>(v => v == null)),
                Times.Exactly(3));
        }

        [Fact(DisplayName = "TC-D77: Export with an invalid date throws ArgumentException and writes no audit entry")]
        public async Task ExportDashboardTasksAsync_BadDate_ThrowsWithoutAudit()
        {
            var request = new DashboardRequest { From = "bad", To = "2025-03-16" };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.ExportDashboardTasksAsync(request, 1, "Admin"));

            _auditRepoMock.Verify(a => a.AddAsync(It.IsAny<AuditLog>()), Times.Never);
            _auditRepoMock.Verify(a => a.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-D78: Export over 366 days throws ArgumentException and writes no audit entry")]
        public async Task ExportDashboardTasksAsync_RangeTooLong_ThrowsWithoutAudit()
        {
            var request = new DashboardRequest { From = "2024-01-01", To = "2025-03-01" };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.ExportDashboardTasksAsync(request, 1, "Admin"));

            _auditRepoMock.Verify(a => a.AddAsync(It.IsAny<AuditLog>()), Times.Never);
        }
    }
}
