using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepo;
        private readonly IAuditLogRepository _auditRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IAuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public AuthController(
            IUserRepository userRepo,
            IAuditLogRepository auditRepo,
            IRoleRepository roleRepo,
            IAuthService authService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _userRepo = userRepo;
            _auditRepo = auditRepo;
            _roleRepo = roleRepo;
            _authService = authService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userRepo.GetByEmailWithRoleAsync(request.Email);

            if (user == null) return Unauthorized("Invalid credentials.");

            if (user.IsDeleted) return Unauthorized("Your account has been deactivated.");

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
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {


            // --- Cloudflare Turnstile verification ---
            var secretKey = _configuration["Turnstile:SecretKey"];
            var httpClient = _httpClientFactory.CreateClient();
            var verifyContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", secretKey!),
                new KeyValuePair<string, string>("response", request.TurnstileToken),
                new KeyValuePair<string, string>("remoteip", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "")
            });

            var verifyResponse = await httpClient.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify", verifyContent);

            var verifyBody = await verifyResponse.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(verifyBody);
            var isSuccess = jsonDoc.RootElement.GetProperty("success").GetBoolean();

            if (!isSuccess)
                return BadRequest("Security check failed. Please try again.");
            // -----------------------------------------

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

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            try
            {
                await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
                return Ok(new { message = "Password changed successfully." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("google-login")]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleCallback") };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync("ExternalCookie");
            if (!authenticateResult.Succeeded)
                return BadRequest("External authentication failed.");

            var email = authenticateResult.Principal.FindFirstValue(ClaimTypes.Email);
            var name = authenticateResult.Principal.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrEmpty(email))
                return BadRequest("Email claim is missing from external provider.");

            var user = await _userRepo.GetByEmailWithRoleAsync(email);

            if (user != null && user.IsDeleted)
                return Unauthorized("Your account has been deactivated.");

            if (user == null)
            {
                return Unauthorized("Your account is not registered in the system. Please contact an administrator.");
            }

            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = user!.UserId,
                Action = $"User '{user.Email}' logged in via Google.",
                EntityName = "Users",
                Timestamp = DateTime.UtcNow
            });
            await _auditRepo.SaveAsync();

            var token = _authService.GenerateToken(user, user.Role!.RoleName);

            // Determine frontend URL (in production this should be read from config)
            var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:5173";
            return Redirect($"{frontendUrl}/oauth-callback?token={token}&email={Uri.EscapeDataString(user.Email)}&role={Uri.EscapeDataString(user.Role.RoleName)}");
        }
    }
}

