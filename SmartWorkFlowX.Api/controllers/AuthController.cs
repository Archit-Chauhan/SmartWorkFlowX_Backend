using Microsoft.AspNetCore.Mvc;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepo;
        private readonly IAuditLogRepository _auditRepo;
        private readonly IAuthService _authService;

        public AuthController(
            IUserRepository userRepo,
            IAuditLogRepository auditRepo,
            IAuthService authService)
        {
            _userRepo = userRepo;
            _auditRepo = auditRepo;
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userRepo.GetByEmailWithRoleAsync(request.Email);

            if (user == null) return Unauthorized("Invalid credentials.");

            if (!_authService.VerifyPassword(request.Password, user.PasswordHash))
                return Unauthorized("Invalid credentials.");

            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = user.UserId,
                Action = $"User '{user.Email}' logged in successfully.",
                EntityName = "Users",
                Timestamp = DateTime.UtcNow
            });
            await _auditRepo.SaveAsync();

            var token = _authService.GenerateToken(user, user.Role!.RoleName);
            return Ok(new AuthResponse(token, user.Email, user.Role.RoleName));
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("Email is required.");

            // In production, you would determine origin Url from request headers, e.g. Request.Headers["Origin"]
            // Since frontend is usually on 5173 for Vite or 3000 for React
            var origin = "http://localhost:5173"; 
            if (Request.Headers.TryGetValue("Origin", out var originHeader))
            {
                origin = originHeader.ToString();
            }
            
            await _authService.ForgotPasswordAsync(request.Email, origin);

            // Always return OK to prevent email enumeration
            return Ok(new { message = "If the email exists, a password reset link has been sent." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || 
                string.IsNullOrWhiteSpace(request.Token) || 
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest("All fields are required.");
            }

            try
            {
                await _authService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
                return Ok(new { message = "Password reset successfully." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}

