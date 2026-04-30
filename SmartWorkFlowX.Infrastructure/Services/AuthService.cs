using Microsoft.IdentityModel.Tokens;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Infrastructure.services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _config;
        private readonly IUserRepository _userRepo;
        private readonly IEmailService _emailService;

        public AuthService(IConfiguration config, IUserRepository userRepo, IEmailService emailService)
        {
            _config = config;
            _userRepo = userRepo;
            _emailService = emailService;
        }

        public string GenerateToken(User user, string roleName)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, roleName)
            };

            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"],
                _config["Jwt:Audience"],
                claims,
                expires: DateTime.Now.AddMinutes(60),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string HashPassword(string plainText)
            => BCrypt.Net.BCrypt.HashPassword(plainText);

        public bool VerifyPassword(string plainText, string hash)
            => BCrypt.Net.BCrypt.Verify(plainText, hash);

        public async Task ForgotPasswordAsync(string email, string originUrl)
        {
            var user = await _userRepo.GetByEmailWithRoleAsync(email);
            if (user == null)
            {
                // We shouldn't reveal whether an email exists or not for security reasons.
                // Just return silently.
                return;
            }

            // Generate a secure token
            var tokenBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes);

            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
            await _userRepo.SaveAsync();

            var encodedToken = Uri.EscapeDataString(token);
            var encodedEmail = Uri.EscapeDataString(email);
            var resetLink = $"{originUrl}/reset-password?token={encodedToken}&email={encodedEmail}";

            var htmlBody = $@"
                <h3>Password Reset Request</h3>
                <p>Hello {user.Name},</p>
                <p>You requested a password reset. Click the link below to reset your password. This link is valid for 15 minutes.</p>
                <p><a href='{resetLink}'>Reset Password</a></p>
                <p>If you did not request this, please ignore this email.</p>";

            await _emailService.SendEmailAsync(user.Email, "Reset Your Password", htmlBody);
        }

        public async Task ResetPasswordAsync(string email, string token, string newPassword)
        {
            var user = await _userRepo.GetByEmailWithRoleAsync(email);
            if (user == null)
                throw new ArgumentException("Invalid token or email.");

            if (user.ResetToken != token || user.ResetTokenExpiry == null || user.ResetTokenExpiry < DateTime.UtcNow)
                throw new ArgumentException("Invalid or expired reset token.");

            user.PasswordHash = HashPassword(newPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _userRepo.SaveAsync();
        }
    }
}

