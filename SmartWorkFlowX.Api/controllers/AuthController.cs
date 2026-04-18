using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Interface;
using SmartWorkFlowX.Infrastructure.Data;
using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly SmartWorkflowXDbContext _context;
        private readonly IAuthService _authService;

        public AuthController(SmartWorkflowXDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null) return Unauthorized("Invalid credentials.");

            // Verify BCrypt hashed password
            if (!_authService.VerifyPassword(request.Password, user.PasswordHash))
                return Unauthorized("Invalid credentials.");

            // Audit log for successful login
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = user.UserId,
                Action = $"User '{user.Email}' logged in successfully.",
                EntityName = "Users",
                Timestamp = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var token = _authService.GenerateToken(user, user.Role!.RoleName);
            return Ok(new AuthResponse(token, user.Email, user.Role.RoleName));
        }
    }
}