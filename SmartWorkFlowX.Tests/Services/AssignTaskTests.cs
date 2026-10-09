using Moq;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Tests.Services
{
    /// <summary>Tests for POST /api/Task/assign validation and GET /api/Task/assignable-users mapping (mocked repositories).</summary>
    public class AssignTaskTests
    {
        private readonly Mock<ITaskRepository> _taskRepo = new Mock<ITaskRepository>();
        private readonly Mock<IWorkflowRepository> _workflowRepo = new Mock<IWorkflowRepository>();
        private readonly Mock<ITaskCategoryRepository> _categoryRepo = new Mock<ITaskCategoryRepository>();
        private readonly Mock<IUserRepository> _userRepo = new Mock<IUserRepository>();
        private readonly Mock<IMessagePublisher> _publisher = new Mock<IMessagePublisher>();
        private readonly TaskService _service;

        public AssignTaskTests()
        {
            _publisher.Setup(p => p.PublishSystemEventAsync(It.IsAny<SystemEventMessage>()))
                .Returns(Task.CompletedTask);

            _workflowRepo.Setup(r => r.GetByIdWithStepsAsync(1)).ReturnsAsync(new Workflow
            {
                WorkflowId = 1,
                Title = "Document Review",
                Status = "Active",
                Steps = new List<WorkflowStep> { new WorkflowStep { StepOrder = 1, ApproverRoleId = 2 } }
            });
            _userRepo.Setup(r => r.ActiveUserExistsAsync(10)).ReturnsAsync(true);
            _categoryRepo.Setup(r => r.ExistsActiveAsync(5)).ReturnsAsync(true);

            _service = new TaskService(
                _taskRepo.Object,
                _workflowRepo.Object,
                new Mock<IAuditLogRepository>().Object,
                new Mock<INotificationService>().Object,
                _publisher.Object,
                _categoryRepo.Object,
                _userRepo.Object);
        }

        private static TaskCreateRequest Request(
            string title = "Prepare report",
            string description = "Details",
            string priority = "Medium",
            int assignedTo = 10,
            DateTime? dueDate = null,
            int? categoryId = null)
        {
            return new TaskCreateRequest(title, description, 1, assignedTo, priority, dueDate, categoryId);
        }

        private async Task AssertRejectedAsync(TaskCreateRequest request, string expectedMessage)
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.AssignTaskAsync(request, 1));
            Assert.Equal(expectedMessage, ex.Message);
            _taskRepo.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Never);
            _taskRepo.Verify(r => r.SaveAsync(), Times.Never);
            _publisher.Verify(p => p.PublishSystemEventAsync(It.IsAny<SystemEventMessage>()), Times.Never);
        }

        // ── Validation messages ────────────────────────────────────────────

        [Fact(DisplayName = "TC-AS01: Blank title (after trimming) is rejected")]
        public async Task Assign_BlankTitle_Rejected()
        {
            await AssertRejectedAsync(Request(title: "   "), "Task title is required.");
        }

        [Fact(DisplayName = "TC-AS02: Title longer than 200 characters (trimmed) is rejected; exactly 200 is accepted")]
        public async Task Assign_LongTitle_Rejected()
        {
            await AssertRejectedAsync(Request(title: new string('a', 201)), "Task title must be 200 characters or fewer.");

            // 200 characters plus surrounding spaces is fine because the title is trimmed first.
            await _service.AssignTaskAsync(Request(title: "  " + new string('a', 200) + "  "), 1);
            _taskRepo.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Once);
        }

        [Fact(DisplayName = "TC-AS03: Description longer than 2000 characters is rejected")]
        public async Task Assign_LongDescription_Rejected()
        {
            await AssertRejectedAsync(Request(description: new string('d', 2001)), "Description must be 2000 characters or fewer.");
        }

        [Theory(DisplayName = "TC-AS04: Priority must be exactly Low, Medium or High")]
        [InlineData("")]
        [InlineData("low")]
        [InlineData("HIGH")]
        [InlineData("Urgent")]
        [InlineData("High ")]
        public async Task Assign_InvalidPriority_Rejected(string priority)
        {
            await AssertRejectedAsync(Request(priority: priority), "Priority must be Low, Medium or High.");
        }

        [Theory(DisplayName = "TC-AS05: Valid priorities are accepted")]
        [InlineData("Low")]
        [InlineData("Medium")]
        [InlineData("High")]
        public async Task Assign_ValidPriority_Accepted(string priority)
        {
            var id = await _service.AssignTaskAsync(Request(priority: priority), 1);
            Assert.Equal(0, id); // mocked repository never assigns an id
            _taskRepo.Verify(r => r.AddAsync(It.Is<TaskItem>(t => t.Priority == priority)), Times.Once);
        }

        [Fact(DisplayName = "TC-AS06: Non-existent or soft-deleted assignee is rejected")]
        public async Task Assign_UnknownAssignee_Rejected()
        {
            // ActiveUserExistsAsync(99) is not set up, so it returns false (same as a deleted user).
            await AssertRejectedAsync(Request(assignedTo: 99), "The selected person was not found or is deactivated.");
        }

        [Fact(DisplayName = "TC-AS07: Unknown category is rejected")]
        public async Task Assign_UnknownCategory_Rejected()
        {
            await AssertRejectedAsync(Request(categoryId: 77), "The selected category was not found.");
        }

        [Fact(DisplayName = "TC-AS08: Known category is accepted and stored")]
        public async Task Assign_KnownCategory_Accepted()
        {
            await _service.AssignTaskAsync(Request(categoryId: 5), 1);
            _taskRepo.Verify(r => r.AddAsync(It.Is<TaskItem>(t => t.CategoryId == 5)), Times.Once);
        }

        // ── Due date tolerance ─────────────────────────────────────────────

        [Fact(DisplayName = "TC-AS09: Due date two days ago (UTC) is rejected")]
        public async Task Assign_DueTwoDaysAgo_Rejected()
        {
            var due = DateTime.UtcNow.Date.AddDays(-2).AddHours(12);
            await AssertRejectedAsync(Request(dueDate: due), "The due date cannot be in the past.");
        }

        [Fact(DisplayName = "TC-AS10: Due date yesterday (UTC) is accepted (one-day tolerance)")]
        public async Task Assign_DueYesterday_Accepted()
        {
            var due = DateTime.UtcNow.Date.AddDays(-1);
            await _service.AssignTaskAsync(Request(dueDate: due), 1);
            _taskRepo.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Once);
        }

        [Fact(DisplayName = "TC-AS11: Due date today, future and null are accepted")]
        public async Task Assign_DueTodayFutureOrNull_Accepted()
        {
            await _service.AssignTaskAsync(Request(dueDate: DateTime.UtcNow.Date), 1);
            await _service.AssignTaskAsync(Request(dueDate: DateTime.UtcNow.AddDays(30)), 1);
            await _service.AssignTaskAsync(Request(dueDate: null), 1);
            _taskRepo.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Exactly(3));
        }

        // ── Stored values and happy path ───────────────────────────────────

        [Fact(DisplayName = "TC-AS12: Trimmed title and trimmed description are stored; null description becomes empty")]
        public async Task Assign_StoresTrimmedValues()
        {
            await _service.AssignTaskAsync(Request(title: "  Prepare report  ", description: "  some text  "), 1);
            await _service.AssignTaskAsync(new TaskCreateRequest("Second", null!, 1, 10, "Low", null, null), 1);

            _taskRepo.Verify(r => r.AddAsync(It.Is<TaskItem>(t => t.Title == "Prepare report" && t.Description == "some text")), Times.Once);
            _taskRepo.Verify(r => r.AddAsync(It.Is<TaskItem>(t => t.Title == "Second" && t.Description == "")), Times.Once);
        }

        [Fact(DisplayName = "TC-AS13: Valid request saves the task and publishes TaskAssigned")]
        public async Task Assign_Valid_SavesAndPublishes()
        {
            await _service.AssignTaskAsync(Request(title: " Prepare report ", dueDate: DateTime.UtcNow.AddDays(3), categoryId: 5), 7);

            _taskRepo.Verify(r => r.AddAsync(It.Is<TaskItem>(t =>
                t.Title == "Prepare report" &&
                t.AssignedTo == 10 &&
                t.OriginalAssignedTo == 10 &&
                t.Status == "In Progress" &&
                t.CurrentStepOrder == 0)), Times.Once);
            _taskRepo.Verify(r => r.SaveAsync(), Times.Once);
            _publisher.Verify(p => p.PublishSystemEventAsync(It.Is<SystemEventMessage>(m =>
                m.EventType == "TaskAssigned" &&
                m.TargetUserId == 10 &&
                m.ActedByUserId == 7 &&
                m.NotificationMessage.Contains("'Prepare report'"))), Times.Once);
        }

        [Fact(DisplayName = "TC-AS14: Validation runs before the workflow is loaded")]
        public async Task Assign_InvalidInput_DoesNotReachWorkflowLookup()
        {
            await Assert.ThrowsAsync<ArgumentException>(() => _service.AssignTaskAsync(Request(title: ""), 1));
            _workflowRepo.Verify(r => r.GetByIdWithStepsAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact(DisplayName = "TC-AS15: Missing workflow still throws KeyNotFoundException")]
        public async Task Assign_MissingWorkflow_NotFound()
        {
            var request = new TaskCreateRequest("Title", "Desc", 404, 10, "High", null, null);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.AssignTaskAsync(request, 1));
        }

        // ── Assignable users ───────────────────────────────────────────────

        [Fact(DisplayName = "TC-AS16: Assignable users — active users only, openTaskCount mapped, ordered by name")]
        public async Task GetAssignableUsers_MapsCountsAndOrder()
        {
            var role = new Role { RoleId = 3, RoleName = "Employee" };
            _userRepo.Setup(r => r.GetActiveWithRolesAsync()).ReturnsAsync(new List<User>
            {
                new User { UserId = 2, Name = "Zoe", Email = "zoe@x.com", Role = role },
                new User { UserId = 1, Name = "alice", Email = "alice@x.com", Role = role },
                new User { UserId = 3, Name = "Gone", Email = "gone@x.com", Role = role, IsDeleted = true },
                new User { UserId = 4, Name = "Bob", Email = "bob@x.com", Role = null }
            });
            _taskRepo.Setup(r => r.GetOpenTaskCountsByAssigneeAsync())
                .ReturnsAsync(new Dictionary<int, int> { { 1, 4 }, { 2, 1 }, { 3, 9 } });

            var result = await _service.GetAssignableUsersAsync();

            Assert.Equal(new[] { "alice", "Bob", "Zoe" }, result.Select(u => u.Name).ToArray());
            Assert.Equal(4, result[0].OpenTaskCount);
            Assert.Equal(0, result[1].OpenTaskCount);   // no open tasks: defaults to 0
            Assert.Equal(1, result[2].OpenTaskCount);
            Assert.Equal("Employee", result[0].RoleName);
            Assert.Equal("", result[1].RoleName);       // missing role does not throw
            Assert.Equal("alice@x.com", result[0].Email);
            Assert.DoesNotContain(result, u => u.UserId == 3);
            _taskRepo.Verify(r => r.GetOpenTaskCountsByAssigneeAsync(), Times.Once);
        }
    }
}
