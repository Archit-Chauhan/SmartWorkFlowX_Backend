using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Interface;
using SmartWorkFlowX.Api.Hubs;
using SmartWorkFlowX.Infrastructure.Data;
using SmartWorkFlowX.Domain.Entities;
using System.Security.Claims;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TaskController : ControllerBase
    {
        private readonly SmartWorkflowXDbContext _context;
        private readonly INotificationService _notificationService;

        public TaskController(SmartWorkflowXDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // GET: api/Task/my-tasks — Returns tasks assigned to the logged-in user
        [HttpGet("my-tasks")]
        public async Task<IActionResult> GetMyTasks()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var tasks = await _context.Tasks
                .Include(t => t.Workflow)
                .Where(t => t.AssignedTo == userId && t.Status != "Completed" && t.Status != "Cancelled")
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new {
                    t.TaskId,
                    t.Title,
                    t.Description,
                    t.Status,
                    t.Priority,
                    t.CurrentStepOrder,
                    t.DueDate,
                    t.CreatedAt,
                    WorkflowTitle = t.Workflow != null ? t.Workflow.Title : null
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // GET: api/Task/all — Admin/Manager view of all tasks
        [HttpGet("all")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetAllTasks(
            [FromQuery] string? status,
            [FromQuery] string? priority,
            [FromQuery] int? assignedTo)
        {
            var query = _context.Tasks
                .Include(t => t.Workflow)
                .Include(t => t.Assignee)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(t => t.Status == status);

            if (!string.IsNullOrEmpty(priority))
                query = query.Where(t => t.Priority == priority);

            if (assignedTo.HasValue)
                query = query.Where(t => t.AssignedTo == assignedTo);

            var tasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new {
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
                    WorkflowTitle = t.Workflow != null ? t.Workflow.Title : null,
                    AssigneeName = t.Assignee != null ? t.Assignee.Name : "Unassigned"
                })
                .ToListAsync();

            return Ok(tasks);
        }

        // POST: api/Task/assign — Manager/Admin assigns a task
        [HttpPost("assign")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> AssignTask([FromBody] TaskCreateRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var userId = int.Parse(userIdString);

            var workflow = await _context.Workflows
                .Include(w => w.Steps)
                .FirstOrDefaultAsync(w => w.WorkflowId == request.WorkflowId);

            if (workflow == null)
                return NotFound(new { message = "Workflow not found." });

            if (workflow.Status != "Active")
                return BadRequest(new { message = "Can only assign tasks to Active workflows. Set the workflow to Active first." });

            if (!workflow.Steps.Any())
                return BadRequest(new { message = "Selected workflow has no defined steps." });

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

            _context.Tasks.Add(newTask);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = $"Assigned task '{request.Title}' (Priority={request.Priority}) via workflow '{workflow.Title}' to User ID={request.AssignedTo}.",
                EntityName = "Tasks",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // Notify the assigned user
            await _notificationService.SendNotificationAsync(
                request.AssignedTo,
                $"You have been assigned a new task: '{request.Title}' (Priority: {request.Priority}).");

            return Ok(new { message = "Task assigned successfully", taskId = newTask.TaskId });
        }

        // POST: api/Task/{id}/approve — Approve current step
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveTask(int id, [FromBody] string? comment)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var task = await _context.Tasks
                .Include(t => t.Workflow)
                    .ThenInclude(w => w!.Steps)
                .FirstOrDefaultAsync(t => t.TaskId == id);

            if (task == null) return NotFound(new { message = "Task not found." });

            if (task.AssignedTo != userId)
                return Forbid();

            // Record step history
            _context.TaskStepHistories.Add(new TaskStepHistory
            {
                TaskId = id,
                StepOrder = task.CurrentStepOrder,
                ActedByUserId = userId,
                Action = "Approved",
                Comment = comment,
                ActedAt = DateTime.UtcNow
            });

            var nextStep = task.Workflow?.Steps
                .OrderBy(s => s.StepOrder)
                .FirstOrDefault(s => s.StepOrder > task.CurrentStepOrder);

            if (nextStep != null)
            {
                var nextApprover = await _context.Users
                    .FirstOrDefaultAsync(u => u.RoleId == nextStep.ApproverRoleId);

                if (nextApprover == null)
                    return BadRequest(new { message = "No user found with the required role for the next approval step." });

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

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = $"Approved Task ID={id} at Step {task.CurrentStepOrder}. New Status: {task.Status}.",
                EntityName = "Tasks",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Task updated to: {task.Status}", status = task.Status });
        }

        // POST: api/Task/{id}/reject — Reject current step
        [HttpPost("{id}/reject")]
        public async Task<IActionResult> RejectTask(int id, [FromBody] TaskRejectRequest request)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var task = await _context.Tasks
                .Include(t => t.Workflow)
                    .ThenInclude(w => w!.Steps)
                .FirstOrDefaultAsync(t => t.TaskId == id);

            if (task == null) return NotFound(new { message = "Task not found." });

            if (task.AssignedTo != userId)
                return Forbid();

            var currentStep = task.Workflow?.Steps
                .FirstOrDefault(s => s.StepOrder == task.CurrentStepOrder);

            // Record step history
            _context.TaskStepHistories.Add(new TaskStepHistory
            {
                TaskId = id,
                StepOrder = task.CurrentStepOrder,
                ActedByUserId = userId,
                Action = "Rejected",
                Comment = request.Comment,
                ActedAt = DateTime.UtcNow
            });

            task.RejectedReason = request.Reason;

            if (currentStep?.OnRejectAction == "GoBack" && task.CurrentStepOrder > 1)
            {
                // Move back to previous step
                var previousStep = task.Workflow!.Steps
                    .OrderByDescending(s => s.StepOrder)
                    .FirstOrDefault(s => s.StepOrder < task.CurrentStepOrder);

                if (previousStep != null)
                {
                    var prevApprover = await _context.Users
                        .FirstOrDefaultAsync(u => u.RoleId == previousStep.ApproverRoleId);

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
                // Cancel the task
                task.Status = "Cancelled";
                task.AssignedTo = null;
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = $"Rejected Task ID={id} at Step {task.CurrentStepOrder}. Reason: {request.Reason}. New Status: {task.Status}.",
                EntityName = "Tasks",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Task rejected. Status: {task.Status}", status = task.Status });
        }

        // GET: api/Task/{id}/history — Step-by-step approval trail
        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetTaskHistory(int id)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null) return NotFound(new { message = "Task not found." });

            var history = await _context.TaskStepHistories
                .Include(h => h.ActedByUser)
                .Where(h => h.TaskId == id)
                .OrderBy(h => h.StepOrder)
                .ThenBy(h => h.ActedAt)
                .Select(h => new TaskStepHistoryResponse(
                    h.StepOrder,
                    h.ActedByUser != null ? h.ActedByUser.Name : "Unknown",
                    h.Action,
                    h.Comment,
                    h.ActedAt))
                .ToListAsync();

            return Ok(history);
        }
    }
}