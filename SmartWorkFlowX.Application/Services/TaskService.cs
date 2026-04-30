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

            // Step 0 = Employee work stage (the employee completes the task first)
            // Step 1+ = Approval steps defined in the workflow
            var newTask = new TaskItem
            {
                Title = request.Title,
                Description = request.Description,
                WorkflowId = request.WorkflowId,
                AssignedTo = request.AssignedTo,
                OriginalAssignedTo = request.AssignedTo, // Track original employee for GoBack
                Priority = request.Priority,
                Status = "In Progress",
                CurrentStepOrder = 0, // Start at Step 0 (employee work stage)
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

            // Step 0 = Employee completing their work, Step 1+ = Approver approving
            var isEmployeeStep = task.CurrentStepOrder == 0;
            var actionLabel = isEmployeeStep ? "Completed" : "Approved";

            await _taskRepo.AddHistoryAsync(new TaskStepHistory
            {
                TaskId = taskId,
                StepOrder = task.CurrentStepOrder,
                ActedByUserId = actingUserId,
                Action = actionLabel,
                Comment = comment,
                ActedAt = DateTime.UtcNow
            });

            // Clear any previous rejection reason when moving forward
            task.RejectedReason = null;

            // Find the next approval step (first step with StepOrder > current)
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
                Action = $"{actionLabel} Task ID={taskId} at Step {task.CurrentStepOrder}. New Status: {task.Status}.",
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

            // Step 0 = Employee stage. Employee rejecting cancels the task.
            if (task.CurrentStepOrder == 0)
            {
                task.Status = "Cancelled";
                task.AssignedTo = null;
            }
            else if (currentStep?.OnRejectAction == "GoBack")
            {
                // Find the previous workflow step
                var previousStep = task.Workflow!.Steps
                    .OrderByDescending(s => s.StepOrder)
                    .FirstOrDefault(s => s.StepOrder < task.CurrentStepOrder);

                if (previousStep != null)
                {
                    // Go back to the previous approval step
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
                else if (task.OriginalAssignedTo.HasValue)
                {
                    // No previous workflow step exists → go back to Step 0 (original employee)
                    task.AssignedTo = task.OriginalAssignedTo.Value;
                    task.CurrentStepOrder = 0;
                    task.Status = "In Progress";

                    await _notificationService.SendNotificationAsync(
                        task.OriginalAssignedTo.Value,
                        $"Task '{task.Title}' was rejected and has been sent back to you for revision.");
                }
                else
                {
                    task.Status = "Cancelled";
                    task.AssignedTo = null;
                }
            }
            else
            {
                // OnRejectAction == "Cancel" or no step found
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

        public async Task<List<object>> GetMyActivityAsync(int userId)
        {
            var tasks = await _taskRepo.GetMyActivityAsync(userId);
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
                t.CreatedAt,
                WorkflowTitle = t.Workflow?.Title
            }).ToList();
        }

        public async Task<PaginatedList<object>> GetMyTasksPaginatedAsync(int userId, int page, int pageSize)
        {
            var (tasks, total) = await _taskRepo.GetMyTasksPaginatedAsync(userId, page, pageSize);
            var mapped = tasks.Select(t => (object)new
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

            return new PaginatedList<object>
            {
                Data = mapped,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PaginatedList<object>> GetMyActivityPaginatedAsync(int userId, int page, int pageSize)
        {
            var (tasks, total) = await _taskRepo.GetMyActivityPaginatedAsync(userId, page, pageSize);
            var mapped = tasks.Select(t => (object)new
            {
                t.TaskId,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                t.CurrentStepOrder,
                t.DueDate,
                t.CompletedAt,
                t.CreatedAt,
                WorkflowTitle = t.Workflow?.Title
            }).ToList();

            return new PaginatedList<object>
            {
                Data = mapped,
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }
    }
}


