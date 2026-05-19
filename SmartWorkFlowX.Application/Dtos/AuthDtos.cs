using System.ComponentModel.DataAnnotations;

namespace SmartWorkFlowX.Application.Dtos
{
    public record LoginRequest([Required][EmailAddress] string Email, [Required] string Password);
    public record UserCreateRequest([Required] string Name, [Required][EmailAddress] string Email, [Required][MinLength(6)] string Password, int RoleId);
    public record AuthResponse(string Token, string Email, string Role);
    public record ForgotPasswordRequest([Required][EmailAddress] string Email, [Required] string TurnstileToken);
    public record ResetPasswordRequest([Required][EmailAddress] string Email, [Required] string Token, [Required][MinLength(6)] string NewPassword);
}

