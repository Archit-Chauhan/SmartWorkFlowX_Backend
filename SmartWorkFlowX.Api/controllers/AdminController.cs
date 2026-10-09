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
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] int? roleId = null,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null)
        {
            return Ok(await _adminService.GetPaginatedUsersAsync(page, limit, search, status, roleId, sort, dir));
        }

        // GET: api/Admin/users/export
        [HttpGet("users/export")]
        public async Task<IActionResult> ExportUsers(
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] int? roleId = null)
        {
            var users = await _adminService.GetAllUsersAsync(search, status, roleId);
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
        public async Task<IActionResult> GetAllRoles()
            => Ok(await _adminService.GetAllRolesAsync());

        // POST: api/Admin/users
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateRequest request)
        {
            // Validation (with fixed messages) happens in AdminService and surfaces as ArgumentException -> 400.
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

        // PUT: api/Admin/users/{id}/restore
        [HttpPut("users/{id}/restore")]
        public async Task<IActionResult> RestoreUser(int id)
        {
            await _adminService.RestoreUserAsync(id, GetUserId());
            return Ok(new { message = "User restored successfully" });
        }

        // PUT: api/Admin/users/{id}/role
        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> ChangeUserRole(int id, [FromBody] ChangeUserRoleRequest request)
        {
            var changed = await _adminService.ChangeUserRoleAsync(id, request.RoleId, GetUserId());
            return Ok(new { message = changed ? "Role updated successfully." : "Role unchanged." });
        }

        private int GetUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private static string EscapeCsv(string value)
            => value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }
}

