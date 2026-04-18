using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Infrastructure.Data;
using SmartWorkFlowX.Domain.Entities;
using System.Security.Claims;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Manager,Admin")]
    public class WorkflowController : ControllerBase
    {
        private readonly SmartWorkflowXDbContext _context;

        public WorkflowController(SmartWorkflowXDbContext context)
        {
            _context = context;
        }

        // POST: api/Workflow — Create a new workflow
        [HttpPost]
        public async Task<IActionResult> CreateWorkflow([FromBody] WorkflowCreateRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();
            var userId = int.Parse(userIdString);

            var workflow = new Workflow
            {
                Title = request.Title,
                Description = request.Description,
                CreatedBy = userId,
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

            _context.Workflows.Add(workflow);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = $"Created workflow '{workflow.Title}' (Status: Draft).",
                EntityName = "Workflows",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "Workflow template created", workflowId = workflow.WorkflowId });
        }

        // GET: api/Workflow — List all workflows
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var workflows = await _context.Workflows
                .Select(w => new WorkflowResponse(
                    w.WorkflowId,
                    w.Title,
                    w.Status,
                    w.Steps.Count))
                .ToListAsync();

            return Ok(workflows);
        }

        // GET: api/Workflow/{id} — Get single workflow with full details
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var workflow = await _context.Workflows
                .Include(w => w.Creator)
                .Include(w => w.Steps)
                    .ThenInclude(s => s.ApproverRole)
                .FirstOrDefaultAsync(w => w.WorkflowId == id);

            if (workflow == null)
                return NotFound(new { message = "Workflow not found." });

            var response = new WorkflowDetailResponse(
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
                    .ToList()
            );

            return Ok(response);
        }

        // PUT: api/Workflow/{id} — Update workflow (replaces steps)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWorkflow(int id, [FromBody] WorkflowUpdateRequest request)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();
            var userId = int.Parse(userIdString);

            var workflow = await _context.Workflows
                .Include(w => w.Steps)
                .FirstOrDefaultAsync(w => w.WorkflowId == id);

            if (workflow == null)
                return NotFound(new { message = "Workflow not found." });

            // Cannot edit a workflow that has active tasks running on it
            var hasActiveTasks = await _context.Tasks
                .AnyAsync(t => t.WorkflowId == id && t.Status == "In Progress");

            if (hasActiveTasks)
                return BadRequest(new { message = "Cannot modify a workflow with active in-progress tasks." });

            workflow.Title = request.Title;
            workflow.Description = request.Description;
            workflow.Status = request.Status;

            // Replace all steps
            _context.WorkflowSteps.RemoveRange(workflow.Steps);
            workflow.Steps = request.Steps.Select(s => new WorkflowStep
            {
                WorkflowId = id,
                StepOrder = s.StepOrder,
                StepName = s.StepName,
                Description = s.Description,
                ApproverRoleId = s.ApproverRoleId,
                OnRejectAction = s.OnRejectAction,
                EscalationHours = s.EscalationHours
            }).ToList();

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = $"Updated workflow '{workflow.Title}' (ID={id}, Status={request.Status}).",
                EntityName = "Workflows",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "Workflow updated successfully." });
        }

        // DELETE: api/Workflow/{id} — Soft-delete (set to Inactive)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWorkflow(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();
            var userId = int.Parse(userIdString);

            var workflow = await _context.Workflows.FindAsync(id);

            if (workflow == null)
                return NotFound(new { message = "Workflow not found." });

            var hasActiveTasks = await _context.Tasks
                .AnyAsync(t => t.WorkflowId == id && t.Status == "In Progress");

            if (hasActiveTasks)
                return BadRequest(new { message = "Cannot deactivate a workflow with active in-progress tasks." });

            workflow.Status = "Inactive";

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = $"Deactivated (soft-deleted) workflow '{workflow.Title}' (ID={id}).",
                EntityName = "Workflows",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "Workflow deactivated successfully." });
        }

        // POST: api/Workflow/{id}/clone — Clone as new Draft template
        [HttpPost("{id}/clone")]
        public async Task<IActionResult> CloneWorkflow(int id)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();
            var userId = int.Parse(userIdString);

            var source = await _context.Workflows
                .Include(w => w.Steps)
                .FirstOrDefaultAsync(w => w.WorkflowId == id);

            if (source == null)
                return NotFound(new { message = "Source workflow not found." });

            var clone = new Workflow
            {
                Title = $"{source.Title} (Copy)",
                Description = source.Description,
                CreatedBy = userId,
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

            _context.Workflows.Add(clone);

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Action = $"Cloned workflow '{source.Title}' (ID={id}) to new Draft '{clone.Title}'.",
                EntityName = "Workflows",
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "Workflow cloned successfully.", newWorkflowId = clone.WorkflowId });
        }
    }
}