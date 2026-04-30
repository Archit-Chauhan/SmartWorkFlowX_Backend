namespace SmartWorkFlowX.Application.Dtos
{
    public record LoginRequest(string Email, string Password);
    public record UserCreateRequest(string Name, string Email, string Password, int RoleId);
    public record AuthResponse(string Token, string Email, string Role);
    public record ForgotPasswordRequest(string Email);
    public record ResetPasswordRequest(string Email, string Token, string NewPassword);
}

