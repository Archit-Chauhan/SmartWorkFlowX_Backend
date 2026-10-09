using System.ComponentModel.DataAnnotations;

namespace SmartWorkFlowX.Application.Dtos
{
    public record LoginRequest([Required][EmailAddress] string Email, [Required] string Password);
    // Used only by the Admin "create user" endpoint. No validation attributes on purpose: AdminService
    // validates with fixed messages (UserCreateValidator) so the API returns the same error shape as
    // every other ArgumentException. The members are nullable so a missing JSON field reaches that
    // validation instead of being rejected by MVC's implicit [Required] with a different error body.
    public record UserCreateRequest(string? Name, string? Email, string? Password, int RoleId);
    public record AuthResponse(string Token, string Email, string Role);
    public record ForgotPasswordRequest([Required][EmailAddress] string Email, [Required] string TurnstileToken);
    public record ResetPasswordRequest([Required][EmailAddress] string Email, [Required] string Token, [Required][MinLength(6)] string NewPassword);
    public record ChangePasswordRequest([Required] string CurrentPassword, [Required][MinLength(6)] string NewPassword);
}

