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

        public AdminService(
            IUserRepository userRepo,
            IRoleRepository roleRepo,
            IAuditLogRepository auditRepo,
            IAuthService authService)
        {
            _userRepo = userRepo;
            _roleRepo = roleRepo;
            _auditRepo = auditRepo;
            _authService = authService;
        }

        public async Task<List<object>> GetAllUsersAsync()
        {
            var users = await _userRepo.GetAllWithRolesAsync();
            return users.Select(u => (object)new
            {
                u.UserId,
                u.Name,
                u.Email,
                RoleName = u.Role?.RoleName ?? "No Role",
                u.RoleId,
                u.CreatedAt
            }).ToList();
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

            return user.UserId;
        }

        public async Task DeleteUserAsync(int targetUserId, int actingUserId)
        {
            if (targetUserId == actingUserId)
                throw new ArgumentException("You cannot delete your own account.");

            var user = await _userRepo.GetByIdAsync(targetUserId)
                ?? throw new KeyNotFoundException("User not found.");

            _userRepo.Remove(user);
            await _auditRepo.AddAsync(new AuditLog
            {
                UserId = actingUserId,
                Action = $"Admin deleted user '{user.Email}' (ID={targetUserId}).",
                EntityName = "Users",
                Timestamp = DateTime.UtcNow
            });
            await _userRepo.SaveAsync();
        }
    }
}


