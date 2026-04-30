using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using System.Security.Claims;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Manager,Admin")]
    public class WorkflowController : ControllerBase
    {
        private readonly IWorkflowService _workflowService;

        public WorkflowController(IWorkflowService workflowService)
        {
            _workflowService = workflowService;
        }

        // GET: api/Workflow
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10)
        {
            return Ok(await _workflowService.GetPaginatedAsync(page, limit));
        }

        // GET: api/Workflow/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
            => Ok(await _workflowService.GetByIdAsync(id));

        // POST: api/Workflow
        [HttpPost]
        public async Task<IActionResult> CreateWorkflow([FromBody] WorkflowCreateRequest request)
        {
            var userId = GetUserId();
            var workflowId = await _workflowService.CreateAsync(request, userId);
            return Ok(new { message = "Workflow template created", workflowId });
        }

        // PUT: api/Workflow/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateWorkflow(int id, [FromBody] WorkflowUpdateRequest request)
        {
            await _workflowService.UpdateAsync(id, request, GetUserId());
            return Ok(new { message = "Workflow updated successfully." });
        }

        // DELETE: api/Workflow/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteWorkflow(int id)
        {
            await _workflowService.DeactivateAsync(id, GetUserId());
            return Ok(new { message = "Workflow deactivated successfully." });
        }

        // POST: api/Workflow/{id}/clone
        [HttpPost("{id}/clone")]
        public async Task<IActionResult> CloneWorkflow(int id)
        {
            var newWorkflowId = await _workflowService.CloneAsync(id, GetUserId());
            return Ok(new { message = "Workflow cloned successfully.", newWorkflowId });
        }

        private int GetUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}

