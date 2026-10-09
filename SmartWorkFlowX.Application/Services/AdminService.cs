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

        private const string AdminRoleName = "Admin";

        public async Task<List<object>> GetAllUsersAsync(string? search = null, string? status = null, int? roleId = null)
        {
            string cleanStatus = UserListQueryParser.ParseStatus(status);
            string? cleanSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var users = await _userRepo.GetAllWithRolesAsync(cleanSearch, cleanStatus, roleId);
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

        public async Task<UsersPagedResponse> GetPaginatedUsersAsync(
            int page, int limit, string? search = null, string? status = null,
            int? roleId = null, string? sort = null, string? dir = null)
        {
            var query = UserListQueryParser.Parse(page, limit, search, status, roleId, sort, dir);
            var result = await _userRepo.GetUserListAsync(query);

            var ids = result.Users.Select(u => u.UserId).ToList();
            var openCounts = await _userRepo.GetOpenTaskCountsAsync(ids);

            var mapped = result.Users.Select(u => (object)new
            {
                u.UserId,
                u.Name,
                u.Email,
                RoleName = u.Role?.RoleName ?? "No Role",
                u.RoleId,
                u.CreatedAt,
                u.IsDeleted,
                u.DeletedAt,
                OpenTaskCount = (openCounts != null && openCounts.TryGetValue(u.UserId, out var open)) ? open : 0
            }).ToList();

            return new UsersPagedResponse
            {
                Data = mapped,
                Total = result.Total,
                Page = query.Page,
                PageSize = query.Limit,
                Counts = result.Counts
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
            // Validation order is fixed by the contract (USERS_CONTRACT.md section 4).
            UserCreateValidator.ValidateIdentity(request.Name, request.Email, out string name, out string email);

            var existing = await _userRepo.GetByEmailWithRoleAsync(email);
            if (existing != null)
            {
                if (existing.IsDeleted)
                    throw new ArgumentException("A deactivated user with this email already exists. Restore them instead.");
                throw new ArgumentException("A user with this email already exists.");
            }

            UserCreateValidator.ValidatePassword(request.Password);
            string password = request.Password!;

            var role = await _roleRepo.GetByIdAsync(request.RoleId);
            if (role == null)
                throw new ArgumentException("The selected role was not found.");

            var user = new User
            {
                Name = name,
                Email = email,
                PasswordHash = _authService.HashPassword(password),
                RoleId = request.RoleId
            };

            await _userRepo.AddAsync(user);
            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Admin created user '{email}' with RoleId={request.RoleId}.",
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
                                Hello <strong>{name}</strong>,
                            </p>
                            
                            <p style='font-size: 15px; margin-bottom: 20px;'>
                                Your account has been successfully created by an administrator. You now have access to SmartWorkFlowX.
                            </p>
                            
                            <div style='background-color: #ecf0f1; padding: 20px; border-left: 4px solid #3498db; margin: 25px 0; border-radius: 4px;'>
                                <h3 style='color: #2c3e50; margin-top: 0; font-size: 16px;'>Your Account Details:</h3>
                                <p style='margin: 8px 0;'><strong>Email Address:</strong> {email}</p>
                                <p style='margin: 8px 0;'><strong>Password:</strong> {password}</p>
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

                await _emailService.SendEmailAsync(email, emailSubject, emailBody);
            }
            catch (Exception ex)
            {
                // Log the exception but don't fail the user creation if email sending fails
                System.Console.WriteLine($"Failed to send registration email to {email}: {ex.Message}");
            }

            return user.UserId;
        }

        public async Task DeleteUserAsync(int targetUserId, int actingUserId)
        {
            if (targetUserId == actingUserId)
                throw new ArgumentException("You cannot delete your own account.");

            var user = await _userRepo.GetByIdIncludingDeletedAsync(targetUserId);
            if (user == null || user.IsDeleted)
                throw new KeyNotFoundException("User not found.");

            if (user.Role != null && user.Role.RoleName == AdminRoleName)
            {
                var activeAdmins = await _userRepo.CountActiveAdminsAsync();
                if (activeAdmins <= 1)
                    throw new ArgumentException("The last active Admin cannot be deactivated. Make someone else an Admin first.");
            }

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
            var user = await _userRepo.GetByIdIncludingDeletedAsync(targetUserId)
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

        public async Task<bool> ChangeUserRoleAsync(int targetUserId, int newRoleId, int actingUserId)
        {
            // Rule order is fixed by the contract (USERS_CONTRACT.md section 2).
            var user = await _userRepo.GetByIdIncludingDeletedAsync(targetUserId)
                ?? throw new KeyNotFoundException("User not found.");

            var newRole = await _roleRepo.GetByIdAsync(newRoleId);
            if (newRole == null)
                throw new ArgumentException("The selected role was not found.");

            if (targetUserId == actingUserId)
                throw new ArgumentException("You cannot change your own role.");

            if (user.IsDeleted)
                throw new ArgumentException("Restore the user before changing their role.");

            if (user.RoleId == newRoleId)
                return false;

            string oldRoleName = user.Role?.RoleName ?? "No Role";

            bool isAdminNow = user.Role != null && user.Role.RoleName == AdminRoleName;
            bool staysAdmin = newRole.RoleName == AdminRoleName;
            if (isAdminNow && !staysAdmin)
            {
                var activeAdmins = await _userRepo.CountActiveAdminsAsync();
                if (activeAdmins <= 1)
                    throw new ArgumentException("The last active Admin cannot be demoted. Make someone else an Admin first.");
            }

            user.RoleId = newRoleId;
            user.Role = newRole;

            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Admin changed role of user '{user.Email}' (ID={targetUserId}) from {oldRoleName} to {newRole.RoleName}.",
                EntityName = "Users",
                Timestamp = DateTime.UtcNow
            });
            await _userRepo.SaveAsync();
            return true;
        }
    }
}


