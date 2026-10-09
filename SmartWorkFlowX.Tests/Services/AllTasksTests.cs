using Moq;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Tests.Services
{
    /// <summary>Tests for GET /api/Task/all: query parsing/validation (pure) and the service paths (mocked repository).</summary>
    public class AllTasksTests
    {
        private static readonly DateTime Now = new DateTime(2025, 3, 20, 12, 0, 0, DateTimeKind.Utc);

        private static AllTasksQuery Parse(AllTasksRequest request)
        {
            return AllTasksQueryParser.Parse(request, Now);
        }

        // ── Parser: defaults and clamping ──────────────────────────────────

        [Fact(DisplayName = "TC-AT01: Defaults — limit 20, group all, sort due asc, all five statuses")]
        public void Parse_Defaults()
        {
            var q = Parse(new AllTasksRequest { Page = 1 });

            Assert.Equal(1, q.Page);
            Assert.Equal(20, q.Limit);
            Assert.Equal("due", q.Sort);
            Assert.False(q.Descending);
            Assert.False(q.Overdue);
            Assert.Null(q.Search);
            Assert.Null(q.Priority);
            Assert.Null(q.CategoryId);
            Assert.Null(q.AssignedTo);
            Assert.Equal(Now, q.Now);
            Assert.Equal(5, q.Statuses.Count);
        }

        [Fact(DisplayName = "TC-AT02: Limit is clamped to 1..100")]
        public void Parse_LimitClamped()
        {
            Assert.Equal(100, Parse(new AllTasksRequest { Page = 1, Limit = 500 }).Limit);
            Assert.Equal(1, Parse(new AllTasksRequest { Page = 1, Limit = 0 }).Limit);
            Assert.Equal(1, Parse(new AllTasksRequest { Page = 1, Limit = -5 }).Limit);
            Assert.Equal(50, Parse(new AllTasksRequest { Page = 1, Limit = 50 }).Limit);
        }

        [Fact(DisplayName = "TC-AT03: Page below 1 is rejected")]
        public void Parse_PageZero_Throws()
        {
            Assert.Throws<ArgumentException>(() => Parse(new AllTasksRequest { Page = 0 }));
            Assert.Throws<ArgumentException>(() => Parse(new AllTasksRequest { Page = -1 }));
        }

        [Fact(DisplayName = "TC-AT04: Search text is trimmed; blank means no search; over 100 chars is rejected")]
        public void Parse_Search()
        {
            Assert.Equal("invoice", Parse(new AllTasksRequest { Page = 1, Q = "  invoice  " }).Search);
            Assert.Null(Parse(new AllTasksRequest { Page = 1, Q = "   " }).Search);
            Assert.Equal(100, Parse(new AllTasksRequest { Page = 1, Q = new string('a', 100) }).Search!.Length);
            Assert.Throws<ArgumentException>(() => Parse(new AllTasksRequest { Page = 1, Q = new string('a', 101) }));
        }

        // ── Parser: unknown values ─────────────────────────────────────────

        [Fact(DisplayName = "TC-AT05: Unknown sort / dir / group / status / priority are rejected")]
        public void Parse_UnknownValues_Throw()
        {
            Assert.Throws<ArgumentException>(() => Parse(new AllTasksRequest { Page = 1, Sort = "bogus" }));
            Assert.Throws<ArgumentException>(() => Parse(new AllTasksRequest { Page = 1, Dir = "up" }));
            Assert.Throws<ArgumentException>(() => Parse(new AllTasksRequest { Page = 1, Group = "everything" }));
            Assert.Throws<ArgumentException>(() => Parse(new AllTasksRequest { Page = 1, Status = "Done" }));
            Assert.Throws<ArgumentException>(() => Parse(new AllTasksRequest { Page = 1, Priority = "Urgent" }));
        }

        [Fact(DisplayName = "TC-AT06: Every documented sort, direction and priority is accepted")]
        public void Parse_ValidValues_Accepted()
        {
            foreach (string sort in new[] { "due", "created", "priority", "title", "workflow", "assignee", "status" })
                Assert.Equal(sort, Parse(new AllTasksRequest { Page = 1, Sort = sort }).Sort);

            Assert.True(Parse(new AllTasksRequest { Page = 1, Dir = "desc" }).Descending);
            Assert.False(Parse(new AllTasksRequest { Page = 1, Dir = "asc" }).Descending);

            foreach (string priority in new[] { "Low", "Medium", "High" })
                Assert.Equal(priority, Parse(new AllTasksRequest { Page = 1, Priority = priority }).Priority);
        }

        [Fact(DisplayName = "TC-AT07: Filters are carried into the query")]
        public void Parse_Filters_Carried()
        {
            var q = Parse(new AllTasksRequest { Page = 3, CategoryId = 4, AssignedTo = 9, Overdue = true });

            Assert.Equal(3, q.Page);
            Assert.Equal(4, q.CategoryId);
            Assert.Equal(9, q.AssignedTo);
            Assert.True(q.Overdue);
        }

        // ── Groups and status ──────────────────────────────────────────────

        [Fact(DisplayName = "TC-AT08: Groups map to the right statuses")]
        public void ResolveStatuses_Groups()
        {
            Assert.Equal(new[] { "Pending", "In Progress" }, AllTasksQueryParser.ResolveStatuses("open", null));
            Assert.Equal(new[] { "Completed" }, AllTasksQueryParser.ResolveStatuses("completed", null));
            Assert.Equal(new[] { "Rejected", "Cancelled" }, AllTasksQueryParser.ResolveStatuses("closed", null));
            Assert.Equal(5, AllTasksQueryParser.ResolveStatuses("all", null).Count);
        }

        [Fact(DisplayName = "TC-AT09: Status combines with group by AND")]
        public void ResolveStatuses_StatusAndGroup()
        {
            Assert.Equal(new[] { "In Progress" }, Parse(new AllTasksRequest { Page = 1, Group = "open", Status = "In Progress" }).Statuses);
            Assert.Equal(new[] { "Cancelled" }, Parse(new AllTasksRequest { Page = 1, Status = "Cancelled" }).Statuses);
            Assert.Empty(Parse(new AllTasksRequest { Page = 1, Group = "open", Status = "Completed" }).Statuses);
        }

        // ── Pure helpers ───────────────────────────────────────────────────

        [Fact(DisplayName = "TC-AT10: LIKE wildcards in user text are escaped")]
        public void EscapeLike_EscapesWildcards()
        {
            Assert.Equal("100\\%", AllTasksQueryParser.EscapeLike("100%"));
            Assert.Equal("a\\_b", AllTasksQueryParser.EscapeLike("a_b"));
            Assert.Equal("\\[x]", AllTasksQueryParser.EscapeLike("[x]"));
            Assert.Equal("a\\\\b", AllTasksQueryParser.EscapeLike("a\\b"));
            Assert.Equal("plain", AllTasksQueryParser.EscapeLike("plain"));
        }

        [Fact(DisplayName = "TC-AT11: Tab counts are built from per-status counts, all = open + completed + closed")]
        public void Counts_FromStatusCounts()
        {
            var byStatus = new Dictionary<string, int>
            {
                { "Pending", 3 }, { "In Progress", 8 }, { "Completed", 16 }, { "Rejected", 4 }, { "Cancelled", 6 }
            };

            var counts = AllTasksCounts.FromStatusCounts(byStatus);

            Assert.Equal(11, counts.Open);
            Assert.Equal(16, counts.Completed);
            Assert.Equal(10, counts.Closed);
            Assert.Equal(37, counts.All);
            Assert.Equal(11, AllTasksCounts.Sum(byStatus, new[] { "Pending", "In Progress" }));
            Assert.Equal(0, AllTasksCounts.FromStatusCounts(new Dictionary<string, int>()).All);
        }

        // ── Service ────────────────────────────────────────────────────────

        private static TaskService BuildService(Mock<ITaskRepository> repo)
        {
            return new TaskService(
                repo.Object,
                new Mock<IWorkflowRepository>().Object,
                new Mock<IAuditLogRepository>().Object,
                new Mock<INotificationService>().Object,
                new Mock<IMessagePublisher>().Object,
                new Mock<ITaskCategoryRepository>().Object);
        }

        [Fact(DisplayName = "TC-AT12: Paged path — shape, counts and page info are passed through")]
        public async Task GetAllTasksPagedAsync_ReturnsShape()
        {
            var repo = new Mock<ITaskRepository>();
            var page = new AllTasksPage
            {
                Rows = new List<AllTasksRow>
                {
                    new AllTasksRow { TaskId = 1, Title = "A", AssigneeName = null, AssignedRoleName = "Manager" },
                    new AllTasksRow { TaskId = 2, Title = "B", AssigneeName = "Alice" }
                },
                Total = 37,
                Counts = new AllTasksCounts { All = 37, Open = 11, Completed = 16, Closed = 10 }
            };
            AllTasksQuery? captured = null;
            repo.Setup(r => r.GetAllTasksPagedAsync(It.IsAny<AllTasksQuery>()))
                .Callback<AllTasksQuery>(q => captured = q)
                .ReturnsAsync(page);

            var result = await BuildService(repo).GetAllTasksPagedAsync(
                new AllTasksRequest { Page = 2, Limit = 5, Group = "open", Sort = "title", Dir = "desc", CategoryId = 7 });

            Assert.Equal(37, result.Total);
            Assert.Equal(2, result.Page);
            Assert.Equal(5, result.PageSize);
            Assert.Equal(2, result.Data.Count());
            Assert.Null(result.Data.First().AssigneeName);
            Assert.Equal("Manager", result.Data.First().AssignedRoleName);
            Assert.Equal(11, result.Counts.Open);
            Assert.Equal(16, result.Counts.Completed);
            Assert.Equal(10, result.Counts.Closed);
            Assert.Equal(37, result.Counts.All);

            Assert.NotNull(captured);
            Assert.Equal(2, captured!.Page);
            Assert.Equal(5, captured.Limit);
            Assert.Equal("title", captured.Sort);
            Assert.True(captured.Descending);
            Assert.Equal(7, captured.CategoryId);
            Assert.Equal(new[] { "Pending", "In Progress" }, captured.Statuses);
        }

        [Fact(DisplayName = "TC-AT13: Paged path — invalid input throws ArgumentException and never reaches the repository")]
        public async Task GetAllTasksPagedAsync_InvalidInput_Throws()
        {
            var repo = new Mock<ITaskRepository>();

            await Assert.ThrowsAsync<ArgumentException>(
                () => BuildService(repo).GetAllTasksPagedAsync(new AllTasksRequest { Page = 1, Sort = "bogus" }));

            repo.Verify(r => r.GetAllTasksPagedAsync(It.IsAny<AllTasksQuery>()), Times.Never);
        }

        [Fact(DisplayName = "TC-AT14: Paged path — a page past the end returns empty data with the real total")]
        public async Task GetAllTasksPagedAsync_PastEnd_EmptyData()
        {
            var repo = new Mock<ITaskRepository>();
            repo.Setup(r => r.GetAllTasksPagedAsync(It.IsAny<AllTasksQuery>()))
                .ReturnsAsync(new AllTasksPage { Total = 3, Counts = new AllTasksCounts { All = 3, Open = 3 } });

            var result = await BuildService(repo).GetAllTasksPagedAsync(new AllTasksRequest { Page = 99 });

            Assert.Empty(result.Data);
            Assert.Equal(3, result.Total);
            Assert.Equal(99, result.Page);
        }

        [Fact(DisplayName = "TC-AT15: Legacy path — categoryId reaches the repository and the old shape is kept")]
        public async Task GetAllFilteredAsync_Legacy_PassesCategoryId()
        {
            var repo = new Mock<ITaskRepository>();
            var task = new TaskItem
            {
                TaskId = 1,
                Title = "T",
                Status = "In Progress",
                Priority = "High",
                Workflow = new Workflow { Title = "WF" },
                Assignee = null,
                Category = new TaskCategory { Name = "Ops", ColorHex = "#fff" }
            };
            repo.Setup(r => r.GetAllFilteredAsync("In Progress", "High", 5, 3))
                .ReturnsAsync(new List<TaskItem> { task });

            var result = await BuildService(repo).GetAllFilteredAsync("In Progress", "High", 5, 3);

            Assert.Single(result);
            repo.Verify(r => r.GetAllFilteredAsync("In Progress", "High", 5, 3), Times.Once);
            var json = System.Text.Json.JsonSerializer.Serialize(result[0]);
            Assert.Contains("\"AssigneeName\":\"Unassigned\"", json);
            Assert.Contains("\"WorkflowTitle\":\"WF\"", json);
            Assert.Contains("\"CategoryName\":\"Ops\"", json);
        }
    }
}
