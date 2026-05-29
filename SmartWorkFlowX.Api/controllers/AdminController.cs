using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using System.Security.Claims;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;

        public AdminController(IAdminService adminService)
        {
            _adminService = adminService;
        }

        // GET: api/Admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 10,
            [FromQuery] string? search = null)
        {
            return Ok(await _adminService.GetPaginatedUsersAsync(page, limit, search));
        }

        // GET: api/Admin/users/export
        [HttpGet("users/export")]
        public async Task<IActionResult> ExportUsers([FromQuery] string? search = null)
        {
            var users = await _adminService.GetAllUsersAsync(search);
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("UserId,Name,Email,Role,CreatedAt");
            var jsonStr = System.Text.Json.JsonSerializer.Serialize(users);
            using var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
            foreach (var u in doc.RootElement.EnumerateArray())
            {
                csv.AppendLine($"{u.GetProperty("UserId").GetInt32()},{EscapeCsv(u.GetProperty("Name").GetString()!)},{EscapeCsv(u.GetProperty("Email").GetString()!)},{EscapeCsv(u.GetProperty("RoleName").GetString()!)},{u.GetProperty("CreatedAt").GetString()}");
            }
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "users.csv");
        }

        // GET: api/Admin/roles
        [HttpGet("roles")]
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> GetAllRoles()
            => Ok(await _adminService.GetAllRolesAsync());

        // POST: api/Admin/users
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await _adminService.CreateUserAsync(request, GetUserId());
            return Ok(new { message = "User created successfully", userId });
        }

        // DELETE: api/Admin/users/{id}
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            await _adminService.DeleteUserAsync(id, GetUserId());
            return Ok(new { message = "User deleted successfully" });
        }

        private int GetUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private static string EscapeCsv(string value)
            => value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }
}

