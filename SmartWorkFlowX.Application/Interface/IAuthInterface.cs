using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Application.Interface
{
    public interface IAuthService
    {
        string GenerateToken(User user, string roleName);
        string HashPassword(string plainText);
        bool VerifyPassword(string plainText, string hash);
    }
}