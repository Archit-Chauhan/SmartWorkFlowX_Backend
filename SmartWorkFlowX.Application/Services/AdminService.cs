using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Application.Services
{
    public class AdminService : IAdminService
    {
        private readonly IUserRepository _userRepo;
        private readonly IRoleRepository _roleRepo;
        private readonly IAuditLogRepository _auditRepo;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;

        public AdminService(
            IUserRepository userRepo,
            IRoleRepository roleRepo,
            IAuditLogRepository auditRepo,
            IAuthService authService,
            IEmailService emailService)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _auditRepo = auditRepo;
            _authService = authService;
            _emailService = emailService;
        }

        public async Task<List<object>> GetAllUsersAsync(string? search = null)
        {
            var users = await _userRepo.GetAllWithRolesAsync(search);
            return users.Select(u => (object)new
            {
                u.UserId,
                u.Name,
                u.Email,
                RoleName = u.Role?.RoleName ?? "No Role",
                u.RoleId,
                u.CreatedAt,
                u.IsDeleted,
                u.DeletedAt
            }).ToList();
        }

        public async Task<PaginatedList<object>> GetPaginatedUsersAsync(int page, int limit, string? search = null)
        {
            var (users, total) = await _userRepo.GetPaginatedAsync(page, limit, search);
            var mapped = users.Select(u => (object)new
            {
                u.UserId,
                u.Name,
                u.Email,
                RoleName = u.Role?.RoleName ?? "No Role",
                u.RoleId,
                u.CreatedAt,
                u.IsDeleted,
                u.DeletedAt
            }).ToList();

            return new PaginatedList<object>
            {
                Data = mapped,
                Total = total,
                Page = page,
                PageSize = limit
            };
        }

        public async Task<List<object>> GetAllRolesAsync()
        {
            var roles = await _roleRepo.GetAllAsync();
            return roles.Select(r => (object)new
            {
                r.RoleId,
                r.RoleName
            }).ToList();
        }

        public async Task<int> CreateUserAsync(UserCreateRequest request, int actingUserId)
        {
            if (await _userRepo.EmailExistsAsync(request.Email))
                throw new ArgumentException("A user with this email already exists.");

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = _authService.HashPassword(request.Password),
                RoleId = request.RoleId
            };

            await _userRepo.AddAsync(user);
            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Admin created user '{request.Email}' with RoleId={request.RoleId}.",
                EntityName = "Users",
                Timestamp = DateTime.UtcNow
            });
            await _userRepo.SaveAsync();

            // Send registration notification email to the user
            try
            {
                var emailSubject = "Welcome to SmartWorkFlowX - Account Created";
                var emailBody = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='UTF-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    </head>
                    <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; background-color: #f5f5f5;'>
                        <div style='max-width: 600px; margin: 20px auto; padding: 30px; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                            <div style='text-align: center; margin-bottom: 30px;'>
                                <h1 style='color: #2c3e50; margin: 0; font-size: 28px;'>Welcome to SmartWorkFlowX!</h1>
                            </div>
                            
                            <p style='font-size: 16px; margin-bottom: 20px;'>
                                Hello <strong>{request.Name}</strong>,
                            </p>
                            
                            <p style='font-size: 15px; margin-bottom: 20px;'>
                                Your account has been successfully created by an administrator. You now have access to SmartWorkFlowX.
                            </p>
                            
                            <div style='background-color: #ecf0f1; padding: 20px; border-left: 4px solid #3498db; margin: 25px 0; border-radius: 4px;'>
                                <h3 style='color: #2c3e50; margin-top: 0; font-size: 16px;'>Your Account Details:</h3>
                                <p style='margin: 8px 0;'><strong>Email Address:</strong> {request.Email}</p>
                                <p style='margin: 8px 0;'><strong>Password:</strong> {request.Password}</p>
                                <p style='margin: 8px 0;'><strong>Account Status:</strong> <span style='color: #27ae60; font-weight: bold;'>Active</span></p>
                            </div>
                            
                            <p style='font-size: 15px; margin-bottom: 10px;'>
                                <strong>Next Steps:</strong>
                            </p>
                            <ol style='font-size: 15px; margin-bottom: 20px;'>
                                <li>Log in using the credentials above</li>
                                <li>Update your profile if needed</li>
                                <li>Start using SmartWorkFlowX</li>
                            </ol>
                            
                            <div style='margin-top: 30px; padding-top: 20px; border-top: 1px solid #ecf0f1;'>
                                <p style='font-size: 13px; color: #7f8c8d; margin: 0;'>
                                    If you have any questions or encounter any issues, please reach out to your administrator.
                                </p>
                                <p style='font-size: 13px; color: #7f8c8d; margin: 10px 0 0 0;'>
                                    <strong>SmartWorkFlowX Team</strong>
                                </p>
                            </div>
                        </div>
                        
                        <div style='text-align: center; margin-top: 20px;'>
                            <p style='font-size: 12px; color: #95a5a6;'>This is a transactional email. Please do not reply to this message.</p>
                        </div>
                    </body>
                    </html>";

                await _emailService.SendEmailAsync(request.Email, emailSubject, emailBody);
            }
            catch (Exception ex)
            {
                // Log the exception but don't fail the user creation if email sending fails
                System.Console.WriteLine($"Failed to send registration email to {request.Email}: {ex.Message}");
            }

            return user.UserId;
        }

        public async Task DeleteUserAsync(int targetUserId, int actingUserId)
        {
            if (targetUserId == actingUserId)
                throw new ArgumentException("You cannot delete your own account.");

            var user = await _userRepo.GetByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("User not found.");

            await _userRepo.SoftDeleteAsync(targetUserId);
            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Admin deleted user '{user.Email}' (ID={targetUserId}).",
                EntityName = "Users",
                Timestamp = DateTime.UtcNow
            });
            await _userRepo.SaveAsync();
        }

        public async Task RestoreUserAsync(int targetUserId, int actingUserId)
        {
            var user = await _userRepo.GetByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("User not found.");

            await _userRepo.RestoreAsync(targetUserId);
            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Admin restored user '{user.Email}' (ID={targetUserId}).",
                EntityName = "Users",
                Timestamp = DateTime.UtcNow
            });
            await _userRepo.SaveAsync();
        }
    }
}


