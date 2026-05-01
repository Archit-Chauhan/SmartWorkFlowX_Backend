using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Contract for JWT generation, password hashing and verification.
    /// </summary>
    public interface IAuthService
    {
        string GenerateToken(User user, string roleName);
        string HashPassword(string plainText);
        bool VerifyPassword(string plainText, string hash);
        Task ForgotPasswordAsync(string email, string originUrl);
        Task ResetPasswordAsync(string email, string token, string newPassword);
    }
}


