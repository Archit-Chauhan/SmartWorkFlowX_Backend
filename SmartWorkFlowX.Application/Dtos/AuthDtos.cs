namespace SmartWorkFlowX.Application.Dtos
{
    public record LoginRequest(string Email, string Password);
    public record UserCreateRequest(string Name, string Email, string Password, int RoleId);
    public record AuthResponse(string Token, string Email, string Role);
}