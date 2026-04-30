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
            [FromQuery] int limit = 10)
        {
            return Ok(await _adminService.GetPaginatedUsersAsync(page, limit));
        }

        // GET: api/Admin/roles
        [HttpGet("roles")]
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
    }
}

