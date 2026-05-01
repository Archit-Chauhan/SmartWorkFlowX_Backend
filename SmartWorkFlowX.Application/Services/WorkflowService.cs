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

        public WorkflowService(
            IWorkflowRepository workflowRepo,
            IAuditLogRepository auditRepo,
            IMessagePublisher messagePublisher)
        {
            _workflowRepo = workflowRepo;
            _auditRepo = auditRepo;
            _messagePublisher = messagePublisher;
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
            var mapped = workflows.Select(w => new WorkflowResponse(
                w.WorkflowId, w.Title, w.Status, w.Steps.Count)).ToList();

            return new PaginatedList<WorkflowResponse>
            {
                Data = mapped,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
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
                        s.EscalationHours))
                    .ToList());
        }

        public async Task<int> CreateAsync(WorkflowCreateRequest request, int createdByUserId)
        {
            var workflow = new Workflow
            {
                Title = request.Title,
                Description = request.Description,
                CreatedBy = createdByUserId,
                Status = "Draft",
                Steps = request.Steps.Select(s => new WorkflowStep
                {
                    StepOrder = s.StepOrder,
                    StepName = s.StepName,
                    Description = s.Description,
                    ApproverRoleId = s.ApproverRoleId,
                    OnRejectAction = s.OnRejectAction,
                    EscalationHours = s.EscalationHours
                }).ToList()
            };

            await _workflowRepo.AddAsync(workflow);

            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = createdByUserId,
                Action = $"Created workflow '{workflow.Title}' (Status: Draft).",
                EntityName = "Workflows",
                Timestamp = DateTime.UtcNow
            });

            await _workflowRepo.SaveAsync();

            return workflow.WorkflowId;
        }

        public async Task UpdateAsync(int workflowId, WorkflowUpdateRequest request, int actingUserId)
        {
            var workflow = await _workflowRepo.GetByIdWithStepsAsync(workflowId)
                ?? throw new KeyNotFoundException("Workflow not found.");

            if (await _workflowRepo.HasActiveTasksAsync(workflowId))
                throw new ArgumentException("Cannot modify a workflow with active in-progress tasks.");

            workflow.Title = request.Title;
            workflow.Description = request.Description;
            workflow.Status = request.Status;

            _workflowRepo.RemoveSteps(workflow.Steps);

            workflow.Steps = request.Steps.Select(s => new WorkflowStep
            {
                WorkflowId = workflowId,
                StepOrder = s.StepOrder,
                StepName = s.StepName,
                Description = s.Description,
                ApproverRoleId = s.ApproverRoleId,
                OnRejectAction = s.OnRejectAction,
                EscalationHours = s.EscalationHours
            }).ToList();

            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Updated workflow '{workflow.Title}' (ID={workflowId}, Status={request.Status}).",
                EntityName = "Workflows",
                Timestamp = DateTime.UtcNow
            });

            await _workflowRepo.SaveAsync();

            if (request.Status == "Active")
            {
                await _messagePublisher.PublishBulkNotificationAsync(
                    null,
                    $"Workflow '{workflow.Title}' Activated",
                    actingUserId
                );
            }
        }

        public async Task DeactivateAsync(int workflowId, int actingUserId)
        {
            var workflow = await _workflowRepo.GetByIdWithStepsAsync(workflowId)
                ?? throw new KeyNotFoundException("Workflow not found.");

            if (await _workflowRepo.HasActiveTasksAsync(workflowId))
                throw new ArgumentException("Cannot deactivate a workflow with active in-progress tasks.");

            workflow.Status = "Inactive";

            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Deactivated (soft-deleted) workflow '{workflow.Title}' (ID={workflowId}).",
                EntityName = "Workflows",
                Timestamp = DateTime.UtcNow
            });

            await _workflowRepo.SaveAsync();

            await _messagePublisher.PublishBulkNotificationAsync(
                null,
                $"Workflow '{workflow.Title}' Deactivated",
                actingUserId
            );
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

            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Cloned workflow '{source.Title}' (ID={workflowId}) to new Draft '{clone.Title}'.",
                EntityName = "Workflows",
                Timestamp = DateTime.UtcNow
            });

            await _workflowRepo.SaveAsync();

            return clone.WorkflowId;
        }
    }
}
