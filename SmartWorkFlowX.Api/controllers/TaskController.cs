using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using System.Security.Claims;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TaskController : ControllerBase
    {
        private readonly ITaskService _taskService;

        public TaskController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        // GET: api/Task/my-tasks
        [HttpGet("my-tasks")]
        public async Task<IActionResult> GetMyTasks()
            => Ok(await _taskService.GetMyTasksAsync(GetUserId()));

        // GET: api/Task/my-activity
        [HttpGet("my-activity")]
        public async Task<IActionResult> GetMyActivity()
            => Ok(await _taskService.GetMyActivityAsync(GetUserId()));

        // GET: api/Task/all
        [HttpGet("all")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetAllTasks(
            [FromQuery] string? status,
            [FromQuery] string? priority,
            [FromQuery] int? assignedTo)
            => Ok(await _taskService.GetAllFilteredAsync(status, priority, assignedTo));

        // POST: api/Task/assign
        [HttpPost("assign")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> AssignTask([FromBody] TaskCreateRequest request)
        {
            var taskId = await _taskService.AssignTaskAsync(request, GetUserId());
            return Ok(new { message = "Task assigned successfully", taskId });
        }

        // POST: api/Task/{id}/approve
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveTask(int id, [FromBody] string? comment)
        {
            var status = await _taskService.ApproveTaskAsync(id, GetUserId(), comment);
            return Ok(new { message = $"Task updated to: {status}", status });
        }

        // POST: api/Task/{id}/reject
        [HttpPost("{id}/reject")]
        public async Task<IActionResult> RejectTask(int id, [FromBody] TaskRejectRequest request)
        {
            var status = await _taskService.RejectTaskAsync(id, GetUserId(), request);
            return Ok(new { message = $"Task rejected. Status: {status}", status });
        }

        // GET: api/Task/{id}/history
        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetTaskHistory(int id)
            => Ok(await _taskService.GetHistoryAsync(id));

        private int GetUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}

