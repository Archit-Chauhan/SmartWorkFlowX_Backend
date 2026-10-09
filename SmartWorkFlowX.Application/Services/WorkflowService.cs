using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Application.Services
{
    public class WorkflowService : IWorkflowService
    {
        private readonly IWorkflowRepository _workflowRepo;
        private readonly IAuditLogRepository _auditRepo;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IRoleRepository _roleRepo;

        public WorkflowService(
            IWorkflowRepository workflowRepo,
            IAuditLogRepository auditRepo,
            IMessagePublisher messagePublisher,
            IRoleRepository roleRepo)
        {
            _workflowRepo = workflowRepo;
            _auditRepo = auditRepo;
            _messagePublisher = messagePublisher;
            _roleRepo = roleRepo;
        }

        public async Task<List<WorkflowResponse>> GetAllAsync()
        {
            var workflows = await _workflowRepo.GetAllAsync();
            return workflows.Select(w => new WorkflowResponse(
                w.WorkflowId, w.Title, w.Status, w.Steps.Count)).ToList();
        }

        public async Task<PaginatedList<WorkflowResponse>> GetPaginatedAsync(int page, int pageSize)
        {
            var (workflows, total) = await _workflowRepo.GetPaginatedAsync(page, pageSize);
            var pageItems = workflows.ToList();

            // One grouped COUNT query for the whole page (no N+1).
            var counts = await _workflowRepo.GetActiveTaskCountsAsync(pageItems.Select(w => w.WorkflowId).ToList());

            var mapped = pageItems.Select(w => new WorkflowResponse(
                w.WorkflowId,
                w.Title,
                w.Status,
                w.Steps.Count,
                w.Description,
                w.Creator?.Name ?? "Unknown",
                w.CreatedAt,
                ActiveCountFor(counts, w.WorkflowId),
                w.Steps
                    .OrderBy(st => st.StepOrder)
                    .Select(st => new WorkflowStepSummaryResponse(
                        st.StepOrder,
                        st.StepName,
                        st.ApproverRole?.RoleName ?? "Unknown"))
                    .ToList())).ToList();

            return new PaginatedList<WorkflowResponse>
            {
                Data = mapped,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        private static int ActiveCountFor(Dictionary<int, int>? counts, int workflowId)
        {
            if (counts == null) return 0;
            return counts.TryGetValue(workflowId, out var n) ? n : 0;
        }

        public async Task<WorkflowDetailResponse> GetByIdAsync(int workflowId)
        {
            var workflow = await _workflowRepo.GetByIdWithDetailsAsync(workflowId)
                ?? throw new KeyNotFoundException("Workflow not found.");

            return new WorkflowDetailResponse(
                workflow.WorkflowId,
                workflow.Title,
                workflow.Description,
                workflow.Status,
                workflow.Creator?.Name ?? "Unknown",
                workflow.CreatedAt,
                workflow.Steps
                    .OrderBy(s => s.StepOrder)
                    .Select(s => new WorkflowStepResponse(
                        s.StepId,
                        s.StepOrder,
                        s.StepName,
                        s.Description,
                        s.ApproverRole?.RoleName ?? "Unknown",
                        s.OnRejectAction,
                        s.EscalationHours,
                        s.ApproverRoleId))
                    .ToList());
        }

        public async Task<int> CreateAsync(WorkflowCreateRequest request, int createdByUserId)
        {
            var (title, description, steps) = await ValidateAsync(request.Title, request.Description, request.Steps, null);
            var status = WorkflowRequestValidator.ResolveCreateStatus(request.Status);

            var workflow = new Workflow
            {
                Title = title,
                Description = description,
                CreatedBy = createdByUserId,
                Status = status,
                Steps = BuildSteps(steps, null)
            };

            await _workflowRepo.AddAsync(workflow);

            await _workflowRepo.SaveAsync();

            await _messagePublisher.PublishSystemEventAsync(new SystemEventMessage
            {
                EventType = "WorkflowCreated",
                EntityName = "Workflows",
                ActionDescription = $"Created workflow '{workflow.Title}' (Status: {status}).",
                ActedByUserId = createdByUserId,
                Timestamp = DateTime.UtcNow
            });

            if (status == "Active")
                await PublishActivatedAsync(workflow.Title, createdByUserId);

            return workflow.WorkflowId;
        }

        public async Task UpdateAsync(int workflowId, WorkflowUpdateRequest request, int actingUserId)
        {
            var workflow = await _workflowRepo.GetByIdWithStepsAsync(workflowId)
                ?? throw new KeyNotFoundException("Workflow not found.");

            if (await _workflowRepo.HasActiveTasksAsync(workflowId))
                throw new ArgumentException("Cannot modify a workflow with active in-progress tasks.");

            var (title, description, steps) = await ValidateAsync(request.Title, request.Description, request.Steps, workflowId);
            WorkflowRequestValidator.ValidateUpdateStatus(request.Status);

            workflow.Title = title;
            workflow.Description = description;
            workflow.Status = request.Status!;

            _workflowRepo.RemoveSteps(workflow.Steps);

            workflow.Steps = BuildSteps(steps, workflowId);

            await _workflowRepo.SaveAsync();

            await _messagePublisher.PublishSystemEventAsync(new SystemEventMessage
            {
                EventType = "WorkflowUpdated",
                EntityName = "Workflows",
                ActionDescription = $"Updated workflow '{workflow.Title}' (ID={workflowId}, Status={request.Status}).",
                ActedByUserId = actingUserId,
                Timestamp = DateTime.UtcNow
            });

            if (request.Status == "Active")
                await PublishActivatedAsync(workflow.Title, actingUserId);
        }

        public async Task<string> ActivateAsync(int workflowId, int actingUserId)
        {
            var workflow = await _workflowRepo.GetByIdWithStepsAsync(workflowId)
                ?? throw new KeyNotFoundException("Workflow not found.");

            if (workflow.Steps.Count == 0)
                throw new ArgumentException("A workflow needs at least one step before it can be activated.");

            if (workflow.Status == "Active")
                return "Workflow is already active.";

            workflow.Status = "Active";

            await _workflowRepo.SaveAsync();

            await PublishActivatedAsync(workflow.Title, actingUserId);

            return "Workflow activated successfully.";
        }

        public async Task DeactivateAsync(int workflowId, int actingUserId)
        {
            var workflow = await _workflowRepo.GetByIdWithStepsAsync(workflowId)
                ?? throw new KeyNotFoundException("Workflow not found.");

            if (await _workflowRepo.HasActiveTasksAsync(workflowId))
                throw new ArgumentException("Cannot deactivate a workflow with active in-progress tasks.");

            workflow.Status = "Inactive";

            await _workflowRepo.SaveAsync();

            await _messagePublisher.PublishSystemEventAsync(new SystemEventMessage
            {
                EventType = "WorkflowDeactivated",
                EntityName = "Workflows",
                ActionDescription = $"Deactivated (soft-deleted) workflow '{workflow.Title}' (ID={workflowId}).",
                ActedByUserId = actingUserId,
                NotificationMessage = $"Workflow '{workflow.Title}' Deactivated",
                Timestamp = DateTime.UtcNow
            });

        }

        public async Task<int> CloneAsync(int workflowId, int actingUserId)
        {
            var source = await _workflowRepo.GetByIdWithStepsAsync(workflowId)
                ?? throw new KeyNotFoundException("Source workflow not found.");

            var clone = new Workflow
            {
                Title = $"{source.Title} (Copy)",
                Description = source.Description,
                CreatedBy = actingUserId,
                Status = "Draft",
                CreatedAt = DateTime.UtcNow,
                Steps = source.Steps.Select(s => new WorkflowStep
                {
                    StepOrder = s.StepOrder,
                    StepName = s.StepName,
                    Description = s.Description,
                    ApproverRoleId = s.ApproverRoleId,
                    OnRejectAction = s.OnRejectAction,
                    EscalationHours = s.EscalationHours
                }).ToList()
            };

            await _workflowRepo.AddAsync(clone);

            await _workflowRepo.SaveAsync();

            await _messagePublisher.PublishSystemEventAsync(new SystemEventMessage
            {
                EventType = "WorkflowCloned",
                EntityName = "Workflows",
                ActionDescription = $"Cloned workflow '{source.Title}' (ID={workflowId}) to new Draft '{clone.Title}'.",
                ActedByUserId = actingUserId,
                Timestamp = DateTime.UtcNow
            });

            return clone.WorkflowId;
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private async Task<(string Title, string Description, List<WorkflowStepCreateDto> Steps)> ValidateAsync(
            string? rawTitle,
            string? rawDescription,
            List<WorkflowStepCreateDto>? rawSteps,
            int? excludeWorkflowId)
        {
            var title = WorkflowRequestValidator.ValidateTitle(rawTitle);

            if (await _workflowRepo.TitleExistsAsync(title, excludeWorkflowId))
                throw new ArgumentException(WorkflowRequestValidator.DuplicateTitleMessage);

            var roles = await _roleRepo.GetAllAsync();
            var roleIds = new HashSet<int>();
            if (roles != null)
            {
                foreach (var r in roles) roleIds.Add(r.RoleId);
            }

            var description = WorkflowRequestValidator.ValidateBody(rawDescription, rawSteps, roleIds);
            var steps = WorkflowRequestValidator.Normalise(rawSteps!);

            return (title, description, steps);
        }

        private static List<WorkflowStep> BuildSteps(List<WorkflowStepCreateDto> steps, int? workflowId)
        {
            var result = new List<WorkflowStep>();
            foreach (var s in steps)
            {
                var step = new WorkflowStep
                {
                    StepOrder = s.StepOrder,
                    StepName = s.StepName!,
                    Description = s.Description,
                    ApproverRoleId = s.ApproverRoleId,
                    OnRejectAction = s.OnRejectAction!,
                    EscalationHours = s.EscalationHours
                };
                if (workflowId.HasValue) step.WorkflowId = workflowId.Value;
                result.Add(step);
            }
            return result;
        }

        private Task PublishActivatedAsync(string title, int actingUserId)
        {
            return _messagePublisher.PublishSystemEventAsync(new SystemEventMessage
            {
                EventType = "WorkflowActivated",
                EntityName = "Workflows",
                ActionDescription = $"Activated workflow '{title}'",
                ActedByUserId = actingUserId,
                NotificationMessage = $"Workflow '{title}' Activated",
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
