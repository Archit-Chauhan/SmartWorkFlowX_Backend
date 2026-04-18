using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Interface;
using SmartWorkFlowX.Infrastructure.Data;
using SmartWorkFlowX.Domain.Entities;
using System.Security.Claims;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly SmartWorkflowXDbContext _context;
        private readonly IAuthService _authService;

        public AdminController(SmartWorkflowXDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        // GET: api/Admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _context.Users
                    .Include(u => u.Role)
                    .Select(u => new {
                        u.UserId,
                        u.Name,
                        u.Email,
                        RoleName = u.Role != null ? u.Role.RoleName : "No Role",
                        u.RoleId,
                        u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // GET: api/Admin/roles
        [HttpGet("roles")]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = await _context.Roles.ToListAsync();
            return Ok(roles);
        }

        // POST: api/Admin/users
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                    return BadRequest(new { message = "A user with this email already exists." });

                var user = new User
                {
                    Name = request.Name,
                    Email = request.Email,
                    PasswordHash = _authService.HashPassword(request.Password), // BCrypt hashed
                    RoleId = request.RoleId
                };

                _context.Users.Add(user);

                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserIdClaim != null ? int.Parse(currentUserIdClaim) : 0,
                    Action = $"Admin created user '{request.Email}' with RoleId={request.RoleId}.",
                    EntityName = "Users",
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return Ok(new { message = "User created successfully", userId = user.UserId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // DELETE: api/Admin/users/{id}
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { message = "User not found." });

                var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (currentUserIdClaim == id.ToString())
                    return BadRequest(new { message = "You cannot delete your own account." });

                _context.Users.Remove(user);

                _context.AuditLogs.Add(new AuditLog
                {
                    UserId = currentUserIdClaim != null ? int.Parse(currentUserIdClaim) : 0,
                    Action = $"Admin deleted user '{user.Email}' (ID={id}).",
                    EntityName = "Users",
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return Ok(new { message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }
    }
}