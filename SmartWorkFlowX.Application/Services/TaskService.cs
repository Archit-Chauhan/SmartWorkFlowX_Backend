using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Application.Services
{
    public class TaskService : ITaskService
    {
        private readonly ITaskRepository _taskRepo;
        private readonly IWorkflowRepository _workflowRepo;
        private readonly IAuditLogRepository _auditRepo;
        private readonly INotificationService _notificationService;

        public TaskService(
            ITaskRepository taskRepo,
            IWorkflowRepository workflowRepo,
            IAuditLogRepository auditRepo,
            INotificationService notificationService)
        {
            _taskRepo = taskRepo;
            _workflowRepo = workflowRepo;
            _auditRepo = auditRepo;
            _notificationService = notificationService;
        }

        public async Task<List<object>> GetMyTasksAsync(int userId)
        {
            var tasks = await _taskRepo.GetMyTasksAsync(userId);
            return tasks.Select(t => (object)new
            {
                t.TaskId,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.CurrentStepOrder,
                t.DueDate,
                t.CreatedAt,
                WorkflowTitle = t.Workflow?.Title
            }).ToList();
        }

        public async Task<List<object>> GetAllFilteredAsync(string? status, string? priority, int? assignedTo)
        {
            var tasks = await _taskRepo.GetAllFilteredAsync(status, priority, assignedTo);
            return tasks.Select(t => (object)new
            {
                t.TaskId,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.CurrentStepOrder,
                t.DueDate,
                t.CompletedAt,
                t.RejectedReason,
                t.CreatedAt,
                WorkflowTitle = t.Workflow?.Title,
                AssigneeName = t.Assignee?.Name ?? "Unassigned"
            }).ToList();
        }

        public async Task<int> AssignTaskAsync(TaskCreateRequest request, int actingUserId)
        {
            var workflow = await _workflowRepo.GetByIdWithStepsAsync(request.WorkflowId)
                ?? throw new KeyNotFoundException("Workflow not found.");

            if (workflow.Status != "Active")
                throw new ArgumentException("Can only assign tasks to Active workflows. Set the workflow to Active first.");

            if (!workflow.Steps.Any())
                throw new ArgumentException("Selected workflow has no defined steps.");

            var firstStep = workflow.Steps.OrderBy(s => s.StepOrder).First();

            var newTask = new TaskItem
            {
                Title = request.Title,
                Description = request.Description,
                WorkflowId = request.WorkflowId,
                AssignedTo = request.AssignedTo,
                Priority = request.Priority,
                Status = "In Progress",
                CurrentStepOrder = firstStep.StepOrder,
                DueDate = request.DueDate,
                CreatedAt = DateTime.UtcNow
            };

            await _taskRepo.AddAsync(newTask);
            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Assigned task '{request.Title}' (Priority={request.Priority}) via workflow '{workflow.Title}' to User ID={request.AssignedTo}.",
                EntityName = "Tasks",
                Timestamp = DateTime.UtcNow
            });
            await _taskRepo.SaveAsync();

            await _notificationService.SendNotificationAsync(
                request.AssignedTo,
                $"You have been assigned a new task: '{request.Title}' (Priority: {request.Priority}).");

            return newTask.TaskId;
        }

        public async Task<string> ApproveTaskAsync(int taskId, int actingUserId, string? comment)
        {
            var task = await _taskRepo.GetByIdWithWorkflowAsync(taskId)
                ?? throw new KeyNotFoundException("Task not found.");

            if (task.AssignedTo != actingUserId)
                throw new UnauthorizedAccessException("You are not the current assignee for this task.");

            await _taskRepo.AddHistoryAsync(new TaskStepHistory
            {
                TaskId = taskId,
                StepOrder = task.CurrentStepOrder,
                ActedByUserId = actingUserId,
                Action = "Approved",
                Comment = comment,
                ActedAt = DateTime.UtcNow
            });

            var nextStep = task.Workflow?.Steps
                .OrderBy(s => s.StepOrder)
                .FirstOrDefault(s => s.StepOrder > task.CurrentStepOrder);

            if (nextStep != null)
            {
                var nextApprover = await _taskRepo.GetFirstUserByRoleAsync(nextStep.ApproverRoleId)
                    ?? throw new ArgumentException("No user found with the required role for the next approval step.");

                task.AssignedTo = nextApprover.UserId;
                task.CurrentStepOrder = nextStep.StepOrder;
                task.Status = "In Progress";

                await _notificationService.SendNotificationAsync(
                    nextApprover.UserId,
                    $"Task '{task.Title}' requires your approval at step {nextStep.StepOrder}: {nextStep.StepName}.");
            }
            else
            {
                task.Status = "Completed";
                task.CompletedAt = DateTime.UtcNow;
                task.AssignedTo = null;
            }

            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Approved Task ID={taskId} at Step {task.CurrentStepOrder}. New Status: {task.Status}.",
                EntityName = "Tasks",
                Timestamp = DateTime.UtcNow
            });
            await _taskRepo.SaveAsync();

            return task.Status;
        }

        public async Task<string> RejectTaskAsync(int taskId, int actingUserId, TaskRejectRequest request)
        {
            var task = await _taskRepo.GetByIdWithWorkflowAsync(taskId)
                ?? throw new KeyNotFoundException("Task not found.");

            if (task.AssignedTo != actingUserId)
                throw new UnauthorizedAccessException("You are not the current assignee for this task.");

            var currentStep = task.Workflow?.Steps
                .FirstOrDefault(s => s.StepOrder == task.CurrentStepOrder);

            await _taskRepo.AddHistoryAsync(new TaskStepHistory
            {
                TaskId = taskId,
                StepOrder = task.CurrentStepOrder,
                ActedByUserId = actingUserId,
                Action = "Rejected",
                Comment = request.Comment,
                ActedAt = DateTime.UtcNow
            });

            task.RejectedReason = request.Reason;

            if (currentStep?.OnRejectAction == "GoBack" && task.CurrentStepOrder > 1)
            {
                var previousStep = task.Workflow!.Steps
                    .OrderByDescending(s => s.StepOrder)
                    .FirstOrDefault(s => s.StepOrder < task.CurrentStepOrder);

                if (previousStep != null)
                {
                    var prevApprover = await _taskRepo.GetFirstUserByRoleAsync(previousStep.ApproverRoleId);
                    if (prevApprover != null)
                    {
                        task.AssignedTo = prevApprover.UserId;
                        task.CurrentStepOrder = previousStep.StepOrder;
                        task.Status = "In Progress";

                        await _notificationService.SendNotificationAsync(
                            prevApprover.UserId,
                            $"Task '{task.Title}' was rejected at step {task.CurrentStepOrder + 1} and has been sent back to you (Step {previousStep.StepOrder}: {previousStep.StepName}) for review.");
                    }
                    else
                    {
                        task.Status = "Cancelled";
                        task.AssignedTo = null;
                    }
                }
            }
            else
            {
                task.Status = "Cancelled";
                task.AssignedTo = null;
            }

            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Rejected Task ID={taskId} at Step {task.CurrentStepOrder}. Reason: {request.Reason}. New Status: {task.Status}.",
                EntityName = "Tasks",
                Timestamp = DateTime.UtcNow
            });
            await _taskRepo.SaveAsync();

            return task.Status;
        }

        public async Task<List<TaskStepHistoryResponse>> GetHistoryAsync(int taskId)
        {
            var history = await _taskRepo.GetHistoryAsync(taskId);
            return history.Select(h => new TaskStepHistoryResponse(
                h.StepOrder,
                h.ActedByUser?.Name ?? "Unknown",
                h.Action,
                h.Comment,
                h.ActedAt)).ToList();
        }
    }
}


