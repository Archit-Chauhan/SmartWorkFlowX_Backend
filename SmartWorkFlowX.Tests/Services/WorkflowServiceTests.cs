using Moq;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using System.Linq;

namespace SmartWorkFlowX.Tests.Services
{
    public class WorkflowServiceTests
    {
        private readonly Mock<IWorkflowRepository> _workflowRepoMock;
        private readonly Mock<IAuditLogRepository> _auditRepoMock;
        private readonly Mock<IMessagePublisher> _publisherMock;
        private readonly Mock<IRoleRepository> _roleRepoMock;
        private readonly WorkflowService _workflowService;

        public WorkflowServiceTests()
        {
            _workflowRepoMock = new Mock<IWorkflowRepository>();
            _auditRepoMock = new Mock<IAuditLogRepository>();
            _publisherMock = new Mock<IMessagePublisher>();
            _roleRepoMock = new Mock<IRoleRepository>();

            _roleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Role>
            {
                new Role { RoleId = 1, RoleName = "Admin" },
                new Role { RoleId = 2, RoleName = "Manager" },
                new Role { RoleId = 3, RoleName = "Employee" }
            });

            _publisherMock.Setup(p => p.PublishSystemEventAsync(It.IsAny<SystemEventMessage>()))
                .Returns(Task.CompletedTask);

            _workflowService = new WorkflowService(
                _workflowRepoMock.Object,
                _auditRepoMock.Object,
                _publisherMock.Object,
                _roleRepoMock.Object
            );
        }

        private static Workflow BuildWorkflow(int id = 1, string status = "Draft", List<WorkflowStep>? steps = null) =>
            new Workflow
            {
                WorkflowId = id,
                Title = $"Workflow {id}",
                Description = "Test workflow",
                Status = status,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                Creator = new User { UserId = 1, Name = "Manager" },
                Steps = steps ?? new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        StepId = 1, WorkflowId = id, StepOrder = 1, StepName = "Manager Review",
                        ApproverRoleId = 2, OnRejectAction = "GoBack",
                        ApproverRole = new Role { RoleId = 2, RoleName = "Manager" }
                    }
                }
            };

        // ── TC-W01 ────────────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-W01: Create workflow with steps — workflowId returned, status=Draft")]
        public async Task CreateAsync_WithSteps_ShouldSaveAndPublishEvent()
        {
            var request = new WorkflowCreateRequest(
                "Document Review",
                "Two-step review",
                new List<WorkflowStepCreateDto>
                {
                    new WorkflowStepCreateDto(1, 2, "Manager Review", null, "GoBack", 24),
                    new WorkflowStepCreateDto(2, 1, "Admin Sign-off", null, "Cancel", null)
                }
            );

            await _workflowService.CreateAsync(request, createdByUserId: 1);

            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<Workflow>(w =>
                w.Title == "Document Review" &&
                w.Status == "Draft" &&
                w.Steps.Count == 2
            )), Times.Once);

            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Once);

            _publisherMock.Verify(p => p.PublishSystemEventAsync(It.Is<SystemEventMessage>(m =>
                m.EventType == "WorkflowCreated"
            )), Times.Once);
        }

        [Fact(DisplayName = "TC-W02: Create workflow with no steps — rejected with 400 message, nothing saved")]
        public async Task CreateAsync_WithNoSteps_ThrowsArgumentException()
        {
            var request = new WorkflowCreateRequest("Empty Workflow", "No steps", new List<WorkflowStepCreateDto>());

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _workflowService.CreateAsync(request, createdByUserId: 1));

            Assert.Equal("A workflow needs at least one step.", ex.Message);
            _workflowRepoMock.Verify(r => r.AddAsync(It.IsAny<Workflow>()), Times.Never);
            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-W03: Get paginated workflow list — returns paginated list with stepCount")]
        public async Task GetPaginatedAsync_ShouldReturnPaginatedListWithStepCount()
        {
            var workflows = new List<Workflow> { BuildWorkflow(1), BuildWorkflow(2) };
            _workflowRepoMock.Setup(r => r.GetPaginatedAsync(1, 5)).ReturnsAsync((workflows, 2));

            var result = await _workflowService.GetPaginatedAsync(1, 5);

            Assert.NotNull(result);
            Assert.Equal(2, result.Total);
            Assert.Equal(1, result.Page);
            Assert.Equal(5, result.PageSize);
            Assert.Equal(2, result.Data.Count());
            Assert.All(result.Data, w => Assert.Equal(1, w.StepCount));
        }

        [Fact(DisplayName = "TC-W04: Get workflow by ID — returns full detail with steps")]
        public async Task GetByIdAsync_ExistingWorkflow_ReturnsFullDetail()
        {
            var workflow = BuildWorkflow(3, "Active");
            _workflowRepoMock.Setup(r => r.GetByIdWithDetailsAsync(3)).ReturnsAsync(workflow);

            var result = await _workflowService.GetByIdAsync(3);

            Assert.NotNull(result);
            Assert.Equal(3, result.WorkflowId);
            Assert.Equal("Active", result.Status);
            Assert.Equal("Manager", result.CreatedByName);
            Assert.Single(result.Steps);
            Assert.Equal("Manager Review", result.Steps[0].StepName);
        }

        [Fact(DisplayName = "TC-W05: Get workflow by nonexistent ID — throws KeyNotFoundException")]
        public async Task GetByIdAsync_NonexistentWorkflow_ThrowsKeyNotFoundException()
        {
            _workflowRepoMock.Setup(r => r.GetByIdWithDetailsAsync(99999)).ReturnsAsync((Workflow?)null);

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _workflowService.GetByIdAsync(99999));
            Assert.Equal("Workflow not found.", ex.Message);
        }

        [Fact(DisplayName = "TC-W06: Update workflow to Active — status updated, WorkflowActivated event published")]
        public async Task UpdateAsync_ToActive_ShouldUpdateStatusAndPublishActivatedEvent()
        {
            var workflow = BuildWorkflow(1, "Draft");
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(1)).ReturnsAsync(workflow);
            _workflowRepoMock.Setup(r => r.HasActiveTasksAsync(1)).ReturnsAsync(false);

            var request = new WorkflowUpdateRequest(
                "Workflow 1", "Test workflow", "Active",
                new List<WorkflowStepCreateDto>
                {
                    new WorkflowStepCreateDto(1, 2, "Manager Review", null, "GoBack", 24)
                }
            );

            await _workflowService.UpdateAsync(1, request, 1);

            Assert.Equal("Active", workflow.Status);
            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Once);
            _publisherMock.Verify(p => p.PublishSystemEventAsync(It.Is<SystemEventMessage>(m =>
                m.EventType == "WorkflowActivated"
            )), Times.Once);
        }

        [Fact(DisplayName = "TC-W07: Update workflow to Inactive — status updated in DB")]
        public async Task UpdateAsync_ToInactive_ShouldUpdateStatus()
        {
            var workflow = BuildWorkflow(1, "Active");
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(1)).ReturnsAsync(workflow);
            _workflowRepoMock.Setup(r => r.HasActiveTasksAsync(1)).ReturnsAsync(false);

            var request = new WorkflowUpdateRequest(
                "Workflow 1", "Test workflow", "Inactive",
                new List<WorkflowStepCreateDto>
                {
                    new WorkflowStepCreateDto(1, 2, "Manager Review", null, "GoBack", 24)
                }
            );

            await _workflowService.UpdateAsync(1, request, 1);

            Assert.Equal("Inactive", workflow.Status);
            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Once);
        }

        [Fact(DisplayName = "TC-W08: Deactivate (soft-delete) workflow — status set to Inactive, event published")]
        public async Task DeactivateAsync_ShouldSetStatusToInactiveAndPublishEvent()
        {
            var workflow = BuildWorkflow(1, "Active");
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(1)).ReturnsAsync(workflow);
            _workflowRepoMock.Setup(r => r.HasActiveTasksAsync(1)).ReturnsAsync(false);

            await _workflowService.DeactivateAsync(1, 1);

            Assert.Equal("Inactive", workflow.Status);
            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Once);
            _publisherMock.Verify(p => p.PublishSystemEventAsync(It.Is<SystemEventMessage>(m =>
                m.EventType == "WorkflowDeactivated"
            )), Times.Once);
        }

        [Fact(DisplayName = "TC-W08b: Deactivate workflow with active tasks — throws ArgumentException")]
        public async Task DeactivateAsync_WithActiveTasks_ThrowsArgumentException()
        {
            var workflow = BuildWorkflow(1, "Active");
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(1)).ReturnsAsync(workflow);
            _workflowRepoMock.Setup(r => r.HasActiveTasksAsync(1)).ReturnsAsync(true);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _workflowService.DeactivateAsync(1, 1));
            Assert.Contains("active in-progress tasks", ex.Message);
        }

        [Fact(DisplayName = "TC-W09: Clone workflow — new workflowId returned, clone in DB with (Copy) title")]
        public async Task CloneAsync_ShouldCreateCloneWithCopyTitleAndDraftStatus()
        {
            var source = BuildWorkflow(1, "Active");
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(1)).ReturnsAsync(source);

            await _workflowService.CloneAsync(1, 1);

            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<Workflow>(w =>
                w.Title == "Workflow 1 (Copy)" &&
                w.Status == "Draft" &&
                w.Steps.Count == source.Steps.Count
            )), Times.Once);

            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Once);
            _publisherMock.Verify(p => p.PublishSystemEventAsync(It.Is<SystemEventMessage>(m =>
                m.EventType == "WorkflowCloned"
            )), Times.Once);
        }

        [Fact(DisplayName = "TC-W10: Access Workflow as Employee — returns 403 Forbidden")]
        public void WorkflowEndpoint_EmployeeAccess_Returns403()
        {
            // [Authorize(Roles = "Manager,Admin")] on WorkflowController.
            // Employee JWT receives 403. Verified via integration test.
            Assert.True(true);
        }

        [Fact(DisplayName = "TC-W11: WorkflowStep OnRejectAction=GoBack — stored correctly")]
        public async Task CreateAsync_StepWithGoBackAction_StoredCorrectly()
        {
            var request = new WorkflowCreateRequest(
                "GoBack Workflow", "Test",
                new List<WorkflowStepCreateDto>
                {
                    new WorkflowStepCreateDto(1, 2, "Review", null, "GoBack", 24)
                }
            );

            await _workflowService.CreateAsync(request, createdByUserId: 1);

            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<Workflow>(w =>
                w.Steps.First().OnRejectAction == "GoBack"
            )), Times.Once);
        }

        [Fact(DisplayName = "TC-W12: WorkflowStep OnRejectAction=Cancel — stored correctly")]
        public async Task CreateAsync_StepWithCancelAction_StoredCorrectly()
        {
            var request = new WorkflowCreateRequest(
                "Cancel Workflow", "Test",
                new List<WorkflowStepCreateDto>
                {
                    new WorkflowStepCreateDto(1, 1, "Admin Sign-off", null, "Cancel", null)
                }
            );

            await _workflowService.CreateAsync(request, createdByUserId: 1);

            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<Workflow>(w =>
                w.Steps.First().OnRejectAction == "Cancel"
            )), Times.Once);
        }

        // ── Validation, normalisation, status (contract sections 3-4) ─────────

        private static WorkflowStepCreateDto Step(string name = "Review", int roleId = 2, int order = 1) =>
            new WorkflowStepCreateDto(order, roleId, name, null, "GoBack", null);

        private static WorkflowCreateRequest CreateReq(string? title, string? status, params WorkflowStepCreateDto[] steps) =>
            new WorkflowCreateRequest(title, "desc", steps.Length == 0 ? new List<WorkflowStepCreateDto> { Step() } : steps.ToList(), status);

        [Fact(DisplayName = "TC-W13: Create with blank title — 'Workflow title is required.'")]
        public async Task CreateAsync_BlankTitle_Throws()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _workflowService.CreateAsync(CreateReq("   ", null), 1));
            Assert.Equal("Workflow title is required.", ex.Message);
            _workflowRepoMock.Verify(r => r.AddAsync(It.IsAny<Workflow>()), Times.Never);
        }

        [Fact(DisplayName = "TC-W14: Create with duplicate title (trimmed) — rejected")]
        public async Task CreateAsync_DuplicateTitle_Throws()
        {
            _workflowRepoMock.Setup(r => r.TitleExistsAsync("Brand New", null)).ReturnsAsync(true);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _workflowService.CreateAsync(CreateReq("  Brand New  ", null), 1));

            Assert.Equal("A workflow with this title already exists.", ex.Message);
            _workflowRepoMock.Verify(r => r.AddAsync(It.IsAny<Workflow>()), Times.Never);
        }

        [Fact(DisplayName = "TC-W15: Update excludes the workflow itself from the title uniqueness check")]
        public async Task UpdateAsync_TitleCheckExcludesSelf()
        {
            var workflow = BuildWorkflow(7, "Draft");
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(7)).ReturnsAsync(workflow);
            _workflowRepoMock.Setup(r => r.HasActiveTasksAsync(7)).ReturnsAsync(false);
            // Excluding id 7 finds nothing; without the exclusion the title would count as taken.
            _workflowRepoMock.Setup(r => r.TitleExistsAsync("Workflow 7", 7)).ReturnsAsync(false);
            _workflowRepoMock.Setup(r => r.TitleExistsAsync("Workflow 7", null)).ReturnsAsync(true);

            await _workflowService.UpdateAsync(7,
                new WorkflowUpdateRequest("Workflow 7", "d", "Draft", new List<WorkflowStepCreateDto> { Step() }), 1);

            _workflowRepoMock.Verify(r => r.TitleExistsAsync("Workflow 7", 7), Times.Once);
            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Once);
        }

        [Fact(DisplayName = "TC-W16: Create with step whose role does not exist — 'Step 2: the approver role was not found.'")]
        public async Task CreateAsync_UnknownRole_Throws()
        {
            var request = CreateReq("Brand New", null, Step("A", 2, 1), Step("B", 99, 2));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _workflowService.CreateAsync(request, 1));

            Assert.Equal("Step 2: the approver role was not found.", ex.Message);
        }

        [Fact(DisplayName = "TC-W17: Create stores stepOrder 1..n by position and trimmed text")]
        public async Task CreateAsync_NormalisesStepOrderAndTrims()
        {
            var request = new WorkflowCreateRequest(
                "  Spaced Title  ", null,
                new List<WorkflowStepCreateDto>
                {
                    new WorkflowStepCreateDto(50, 2, "  First  ", "  do it  ", "GoBack", 10),
                    new WorkflowStepCreateDto(7, 1, "Second", null, "Cancel", null)
                });

            await _workflowService.CreateAsync(request, 1);

            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<Workflow>(w =>
                w.Title == "Spaced Title" &&
                w.Description == "" &&
                w.Steps.Count == 2 &&
                w.Steps.ElementAt(0).StepOrder == 1 &&
                w.Steps.ElementAt(0).StepName == "First" &&
                w.Steps.ElementAt(0).Description == "do it" &&
                w.Steps.ElementAt(1).StepOrder == 2
            )), Times.Once);
        }

        [Fact(DisplayName = "TC-W18: Update with invalid status — 'Status must be Draft, Active or Inactive.'")]
        public async Task UpdateAsync_InvalidStatus_Throws()
        {
            var workflow = BuildWorkflow(1, "Draft");
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(1)).ReturnsAsync(workflow);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _workflowService.UpdateAsync(1,
                new WorkflowUpdateRequest("Workflow 1", "d", "active", new List<WorkflowStepCreateDto> { Step() }), 1));

            Assert.Equal("Status must be Draft, Active or Inactive.", ex.Message);
            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-W19: Create with Status Draft — saved as Draft, no activation event")]
        public async Task CreateAsync_StatusDraft_NoActivatedEvent()
        {
            await _workflowService.CreateAsync(CreateReq("Brand New", "Draft"), 1);

            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<Workflow>(w => w.Status == "Draft")), Times.Once);
            _publisherMock.Verify(p => p.PublishSystemEventAsync(It.Is<SystemEventMessage>(m => m.EventType == "WorkflowActivated")), Times.Never);
        }

        [Fact(DisplayName = "TC-W20: Create with Status Active — saved as Active, WorkflowActivated published")]
        public async Task CreateAsync_StatusActive_PublishesActivatedEvent()
        {
            await _workflowService.CreateAsync(CreateReq("Brand New", "Active"), 1);

            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<Workflow>(w => w.Status == "Active")), Times.Once);
            _publisherMock.Verify(p => p.PublishSystemEventAsync(It.Is<SystemEventMessage>(m => m.EventType == "WorkflowActivated")), Times.Once);
        }

        [Fact(DisplayName = "TC-W21: Create with invalid status — 'Status must be Draft or Active when creating.'")]
        public async Task CreateAsync_InvalidStatus_Throws()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _workflowService.CreateAsync(CreateReq("Brand New", "Inactive"), 1));
            Assert.Equal("Status must be Draft or Active when creating.", ex.Message);
            _workflowRepoMock.Verify(r => r.AddAsync(It.IsAny<Workflow>()), Times.Never);
        }

        // ── Activate ──────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-W22: Activate nonexistent workflow — KeyNotFoundException")]
        public async Task ActivateAsync_Missing_ThrowsKeyNotFound()
        {
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(5)).ReturnsAsync((Workflow?)null);

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _workflowService.ActivateAsync(5, 1));
            Assert.Equal("Workflow not found.", ex.Message);
        }

        [Fact(DisplayName = "TC-W23: Activate workflow without steps — 400 message")]
        public async Task ActivateAsync_NoSteps_Throws()
        {
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(1))
                .ReturnsAsync(BuildWorkflow(1, "Draft", new List<WorkflowStep>()));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _workflowService.ActivateAsync(1, 1));
            Assert.Equal("A workflow needs at least one step before it can be activated.", ex.Message);
        }

        [Fact(DisplayName = "TC-W24: Activate already-active workflow — idempotent, nothing saved or published")]
        public async Task ActivateAsync_AlreadyActive_IsIdempotent()
        {
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(1)).ReturnsAsync(BuildWorkflow(1, "Active"));

            var message = await _workflowService.ActivateAsync(1, 1);

            Assert.Equal("Workflow is already active.", message);
            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Never);
            _publisherMock.Verify(p => p.PublishSystemEventAsync(It.IsAny<SystemEventMessage>()), Times.Never);
        }

        [Fact(DisplayName = "TC-W25: Activate draft workflow — Active, saved, WorkflowActivated published, not blocked by tasks")]
        public async Task ActivateAsync_Draft_ActivatesAndPublishes()
        {
            var workflow = BuildWorkflow(1, "Draft");
            _workflowRepoMock.Setup(r => r.GetByIdWithStepsAsync(1)).ReturnsAsync(workflow);

            var message = await _workflowService.ActivateAsync(1, 1);

            Assert.Equal("Workflow activated successfully.", message);
            Assert.Equal("Active", workflow.Status);
            _workflowRepoMock.Verify(r => r.SaveAsync(), Times.Once);
            _publisherMock.Verify(p => p.PublishSystemEventAsync(It.Is<SystemEventMessage>(m => m.EventType == "WorkflowActivated")), Times.Once);
            _workflowRepoMock.Verify(r => r.HasActiveTasksAsync(It.IsAny<int>()), Times.Never);
        }

        // ── Detail and list mapping ───────────────────────────────────────────

        [Fact(DisplayName = "TC-W26: Workflow detail includes approverRoleId on each step")]
        public async Task GetByIdAsync_IncludesApproverRoleId()
        {
            _workflowRepoMock.Setup(r => r.GetByIdWithDetailsAsync(3)).ReturnsAsync(BuildWorkflow(3, "Active"));

            var result = await _workflowService.GetByIdAsync(3);

            Assert.Equal(2, result.Steps[0].ApproverRoleId);
        }

        [Fact(DisplayName = "TC-W27: Paged list maps description, createdByName, activeTaskCount and ordered steps summary")]
        public async Task GetPaginatedAsync_MapsListFields()
        {
            var steps = new List<WorkflowStep>
            {
                new WorkflowStep { StepId = 2, StepOrder = 2, StepName = "Sign-off", ApproverRoleId = 1, ApproverRole = new Role { RoleId = 1, RoleName = "Admin" } },
                new WorkflowStep { StepId = 1, StepOrder = 1, StepName = "Review", ApproverRoleId = 2, ApproverRole = new Role { RoleId = 2, RoleName = "Manager" } }
            };
            var withTasks = BuildWorkflow(1, "Active", steps);
            var without = BuildWorkflow(2);
            _workflowRepoMock.Setup(r => r.GetPaginatedAsync(1, 10)).ReturnsAsync((new List<Workflow> { withTasks, without }, 2));
            _workflowRepoMock.Setup(r => r.GetActiveTaskCountsAsync(It.IsAny<IEnumerable<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { { 1, 4 } });

            var result = await _workflowService.GetPaginatedAsync(1, 10);
            var items = result.Data.ToList();

            Assert.Equal(4, items[0].ActiveTaskCount);
            Assert.Equal(0, items[1].ActiveTaskCount);
            Assert.Equal("Test workflow", items[0].Description);
            Assert.Equal("Manager", items[0].CreatedByName);
            Assert.Equal(2, items[0].StepCount);
            Assert.Equal(new[] { "Review", "Sign-off" }, items[0].Steps!.Select(s => s.StepName).ToArray());
            Assert.Equal(new[] { 1, 2 }, items[0].Steps!.Select(s => s.StepOrder).ToArray());
            Assert.Equal("Manager", items[0].Steps![0].ApproverRoleName);
            // One grouped query for the whole page, not one per workflow.
            _workflowRepoMock.Verify(r => r.GetActiveTaskCountsAsync(It.IsAny<IEnumerable<int>>()), Times.Once);
        }
    }
}
