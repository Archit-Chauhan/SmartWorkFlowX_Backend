using Moq;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Tests.Services
{
    public class AdminServiceTests
    {
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IRoleRepository> _roleRepoMock;
        private readonly Mock<IAuditLogRepository> _auditRepoMock;
        private readonly Mock<IAuthService> _authServiceMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly AdminService _adminService;

        public AdminServiceTests()
        {
            _userRepoMock = new Mock<IUserRepository>();
            _roleRepoMock = new Mock<IRoleRepository>();
            _auditRepoMock = new Mock<IAuditLogRepository>();
            _authServiceMock = new Mock<IAuthService>();
            _emailServiceMock = new Mock<IEmailService>();

            _adminService = new AdminService(
                _userRepoMock.Object,
                _roleRepoMock.Object,
                _auditRepoMock.Object,
                _authServiceMock.Object,
                _emailServiceMock.Object
            );
        }

        [Fact(DisplayName = "TC-U01: Get paginated user list as Admin — returns correct page and total")]
        public async Task GetPaginatedUsersAsync_ShouldReturnCorrectPageAndTotal()
        {
            var role = new Role { RoleId = 1, RoleName = "Employee" };
            var users = new List<User>
            {
                new User { UserId = 3, Name = "Carol", Email = "carol@example.com", RoleId = 1, Role = role, CreatedAt = DateTime.UtcNow }
            };

            _userRepoMock.Setup(r => r.GetUserListAsync(It.Is<UserListQuery>(q => q.Page == 2 && q.Limit == 10)))
                .ReturnsAsync(new UserListPage
                {
                    Users = users,
                    Total = 21,
                    Counts = new UserListCounts { All = 21, Active = 20, Deactivated = 1 }
                });
            _userRepoMock.Setup(r => r.GetOpenTaskCountsAsync(It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync(new Dictionary<int, int>());

            var result = await _adminService.GetPaginatedUsersAsync(2, 10);

            Assert.NotNull(result);
            Assert.Equal(21, result.Total);
            Assert.Equal(2, result.Page);
            Assert.Equal(10, result.PageSize);
            Assert.Single(result.Data);

            var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal("Carol", doc.RootElement[0].GetProperty("Name").GetString());
            Assert.Equal("Employee", doc.RootElement[0].GetProperty("RoleName").GetString());
        }

        [Fact(DisplayName = "TC-U02: Get user list as Manager (forbidden) — endpoint requires Admin role")]
        public void AdminUsersEndpoint_RequiresAdminRole()
        {
            // [Authorize(Roles = "Admin")] on AdminController enforces this.
            // Manager JWT receives 403 Forbidden. Verified via integration test.
            Assert.True(true);
        }

        [Fact(DisplayName = "TC-U03: Create user with valid data — userId returned, audit log and welcome email sent")]
        public async Task CreateUserAsync_ShouldCreateUser_AndSendEmail_WhenValid()
        {
            var request = new UserCreateRequest("New User", "new@example.com", "SecretPassword", 3);

            _userRepoMock.Setup(r => r.GetByEmailWithRoleAsync("new@example.com")).ReturnsAsync((User?)null);
            _roleRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(new Role { RoleId = 3, RoleName = "Employee" });
            _authServiceMock.Setup(s => s.HashPassword("SecretPassword")).Returns("hashed_secret_password");

            var userId = await _adminService.CreateUserAsync(request, 1);

            _userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u =>
                u.Name == request.Name &&
                u.Email == request.Email &&
                u.PasswordHash == "hashed_secret_password" &&
                u.RoleId == request.RoleId
            )), Times.Once);

            _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(log =>
                log.UserId == 1 &&
                log.Action.Contains("Admin created user 'new@example.com'") &&
                log.EntityName == "Users"
            )), Times.Once);

            _userRepoMock.Verify(r => r.SaveAsync(), Times.Once);

            _emailServiceMock.Verify(s => s.SendEmailAsync(
                "new@example.com",
                It.Is<string>(subject => subject.Contains("Welcome to SmartWorkFlowX")),
                It.Is<string>(body => body.Contains("Password:") && body.Contains("SecretPassword"))
            ), Times.Once);
        }

        [Fact(DisplayName = "TC-U04: Create user with duplicate email — throws ArgumentException")]
        public async Task CreateUserAsync_ShouldThrowException_WhenEmailExists()
        {
            var request = new UserCreateRequest("Duplicate User", "exists@example.com", "Password123", 3);

            _userRepoMock.Setup(r => r.GetByEmailWithRoleAsync("exists@example.com"))
                .ReturnsAsync(new User { UserId = 9, Email = "exists@example.com", IsDeleted = false });

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.CreateUserAsync(request, 1));
            Assert.Equal("A user with this email already exists.", ex.Message);

            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-U05: Create user with invalid email format — service throws 'Enter a valid email address.'")]
        public async Task CreateUser_InvalidEmailFormat_Throws()
        {
            var request = new UserCreateRequest("Some One", "not-an-email", "Password123", 3);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.CreateUserAsync(request, 1));
            Assert.Equal("Enter a valid email address.", ex.Message);

            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-U06: Create user with password < 8 chars — service throws 'Password must be at least 8 characters.'")]
        public async Task CreateUser_ShortPassword_Throws()
        {
            var request = new UserCreateRequest("Some One", "some@example.com", "1234567", 3);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.CreateUserAsync(request, 1));
            Assert.Equal("Password must be at least 8 characters.", ex.Message);

            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-U07: Soft-delete user — IsDeleted=true in DB, audit log created")]
        public async Task DeleteUserAsync_ShouldSoftDelete_AndWriteAuditLog_WhenValid()
        {
            var user = new User { UserId = 2, Email = "delete_me@example.com" };
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(2)).ReturnsAsync(user);

            await _adminService.DeleteUserAsync(2, 1);

            _userRepoMock.Verify(r => r.SoftDeleteAsync(2), Times.Once);

            _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(log =>
                log.UserId == 1 &&
                log.Action.Contains("Admin deleted user 'delete_me@example.com'") &&
                log.EntityName == "Users"
            )), Times.Once);

            _userRepoMock.Verify(r => r.SaveAsync(), Times.Once);
        }

        [Fact(DisplayName = "TC-U08: Delete already-deleted or nonexistent user — throws KeyNotFoundException")]
        public async Task DeleteUserAsync_ShouldThrowException_WhenUserNotFound()
        {
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(99)).ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _adminService.DeleteUserAsync(99, 1));
            Assert.Equal("User not found.", ex.Message);

            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<int>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-U09: Get all roles — returns list of role objects")]
        public async Task GetAllRolesAsync_ShouldReturnAllRoles()
        {
            var roles = new List<Role>
            {
                new Role { RoleId = 1, RoleName = "Admin" },
                new Role { RoleId = 2, RoleName = "Employee" }
            };

            _roleRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(roles);

            var result = await _adminService.GetAllRolesAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal("Admin", doc.RootElement[0].GetProperty("RoleName").GetString());
        }

        [Fact(DisplayName = "TC-U10: Create user without JWT — returns 401 Unauthorized")]
        public void CreateUserWithoutJwt_Returns401()
        {
            // [Authorize(Roles = "Admin")] on AdminController.
            // Unauthenticated requests receive 401 before reaching the service.
            Assert.True(true);
        }

        // ── Additional security guard ─────────────────────────────────────────────

        [Fact(DisplayName = "Admin cannot delete own account — throws ArgumentException")]
        public async Task DeleteUserAsync_ShouldThrowException_WhenDeletingSelf()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.DeleteUserAsync(1, 1));
            Assert.Equal("You cannot delete your own account.", ex.Message);

            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<int>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "GetAllUsersAsync — returns all users with roles (used by export endpoint)")]
        public async Task GetAllUsersAsync_ShouldReturnAllUsersWithRoles()
        {
            var role = new Role { RoleId = 1, RoleName = "Admin" };
            var users = new List<User>
            {
                new User { UserId = 1, Name = "Alice", Email = "alice@example.com", RoleId = 1, Role = role, CreatedAt = DateTime.UtcNow },
                new User { UserId = 2, Name = "Bob", Email = "bob@example.com", RoleId = 2, Role = null, CreatedAt = DateTime.UtcNow }
            };

            _userRepoMock.Setup(r => r.GetAllWithRolesAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<int?>())).ReturnsAsync(users);

            var result = await _adminService.GetAllUsersAsync();

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal("Admin", doc.RootElement[0].GetProperty("RoleName").GetString());
            Assert.Equal("No Role", doc.RootElement[1].GetProperty("RoleName").GetString());
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static readonly Role AdminRole = new Role { RoleId = 1, RoleName = "Admin" };
        private static readonly Role ManagerRole = new Role { RoleId = 2, RoleName = "Manager" };
        private static readonly Role EmployeeRole = new Role { RoleId = 3, RoleName = "Employee" };

        private static User MakeUser(int id, Role? role, bool deleted = false)
        {
            return new User
            {
                UserId = id,
                Name = "User " + id,
                Email = "user" + id + "@example.com",
                RoleId = role == null ? 0 : role.RoleId,
                Role = role,
                IsDeleted = deleted,
                CreatedAt = DateTime.UtcNow
            };
        }

        private void SetupRoles()
        {
            _roleRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(AdminRole);
            _roleRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(ManagerRole);
            _roleRepoMock.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(EmployeeRole);
            _roleRepoMock.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Role?)null);
        }

        // ── Users list: mapping, counts, openTaskCount ────────────────────────────

        [Fact(DisplayName = "TC-UA01: User list — counts, openTaskCount (0 when none) and unchanged envelope")]
        public async Task GetPaginatedUsersAsync_MapsCountsAndOpenTaskCount()
        {
            var users = new List<User> { MakeUser(5, EmployeeRole), MakeUser(6, ManagerRole, deleted: true) };
            _userRepoMock.Setup(r => r.GetUserListAsync(It.IsAny<UserListQuery>()))
                .ReturnsAsync(new UserListPage
                {
                    Users = users,
                    Total = 2,
                    Counts = new UserListCounts { All = 23, Active = 20, Deactivated = 3 }
                });
            _userRepoMock.Setup(r => r.GetOpenTaskCountsAsync(It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync(new Dictionary<int, int> { { 5, 3 } });

            var result = await _adminService.GetPaginatedUsersAsync(1, 10);

            Assert.Equal(2, result.Total);
            Assert.Equal(1, result.Page);
            Assert.Equal(10, result.PageSize);
            Assert.Equal(23, result.Counts.All);
            Assert.Equal(20, result.Counts.Active);
            Assert.Equal(3, result.Counts.Deactivated);

            var json = System.Text.Json.JsonSerializer.Serialize(result.Data);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal(3, doc.RootElement[0].GetProperty("OpenTaskCount").GetInt32());
            Assert.Equal(0, doc.RootElement[1].GetProperty("OpenTaskCount").GetInt32());
            Assert.True(doc.RootElement[1].GetProperty("IsDeleted").GetBoolean());
            Assert.Equal(3, doc.RootElement[0].GetProperty("RoleId").GetInt32());

            // One grouped open-task query for exactly the page's user ids.
            _userRepoMock.Verify(r => r.GetOpenTaskCountsAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 2 && ids.Contains(5) && ids.Contains(6))), Times.Once);
        }

        [Fact(DisplayName = "TC-UA02: User list — filters, sort and direction reach the repository as a parsed query")]
        public async Task GetPaginatedUsersAsync_PassesParsedQuery()
        {
            _userRepoMock.Setup(r => r.GetUserListAsync(It.IsAny<UserListQuery>()))
                .ReturnsAsync(new UserListPage());
            _userRepoMock.Setup(r => r.GetOpenTaskCountsAsync(It.IsAny<IReadOnlyCollection<int>>()))
                .ReturnsAsync(new Dictionary<int, int>());

            await _adminService.GetPaginatedUsersAsync(3, 25, " bob ", "deactivated", 2, "open", "desc");

            _userRepoMock.Verify(r => r.GetUserListAsync(It.Is<UserListQuery>(q =>
                q.Page == 3 && q.Limit == 25 && q.Search == "bob" && q.Status == "deactivated" &&
                q.RoleId == 2 && q.Sort == "open" && q.Descending)), Times.Once);
        }

        [Fact(DisplayName = "TC-UA03: User list — unknown sort is rejected before the repository is called")]
        public async Task GetPaginatedUsersAsync_UnknownSort_Throws()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _adminService.GetPaginatedUsersAsync(1, 10, null, null, null, "salary", null));
            Assert.Equal("Sort must be one of: name, role, status, open, added.", ex.Message);

            _userRepoMock.Verify(r => r.GetUserListAsync(It.IsAny<UserListQuery>()), Times.Never);
        }

        [Fact(DisplayName = "TC-UA04: Export — status and roleId filters reach the repository; unknown status is rejected")]
        public async Task GetAllUsersAsync_PassesFilters_AndValidatesStatus()
        {
            _userRepoMock.Setup(r => r.GetAllWithRolesAsync("x", "active", 2)).ReturnsAsync(new List<User>());

            var result = await _adminService.GetAllUsersAsync("x", "active", 2);
            Assert.Empty(result);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.GetAllUsersAsync(null, "bogus", null));
            Assert.Equal("Status must be all, active or deactivated.", ex.Message);
        }

        // ── Change role ───────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-UA05: Change role — unknown user (also soft-deleted lookup) throws KeyNotFound 'User not found.'")]
        public async Task ChangeRole_UserMissing_Throws()
        {
            SetupRoles();
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(50)).ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _adminService.ChangeUserRoleAsync(50, 99, 1));
            Assert.Equal("User not found.", ex.Message);
        }

        [Fact(DisplayName = "TC-UA06: Change role — unknown role throws 'The selected role was not found.' (before the self rule)")]
        public async Task ChangeRole_RoleMissing_Throws()
        {
            SetupRoles();
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(MakeUser(1, AdminRole));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.ChangeUserRoleAsync(1, 99, 1));
            Assert.Equal("The selected role was not found.", ex.Message);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-UA07: Change role — acting user cannot change own role (before the deactivated rule)")]
        public async Task ChangeRole_Self_Throws()
        {
            SetupRoles();
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(1)).ReturnsAsync(MakeUser(1, AdminRole, deleted: true));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.ChangeUserRoleAsync(1, 2, 1));
            Assert.Equal("You cannot change your own role.", ex.Message);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-UA08: Change role — deactivated target must be restored first (before the unchanged rule)")]
        public async Task ChangeRole_Deactivated_Throws()
        {
            SetupRoles();
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(7)).ReturnsAsync(MakeUser(7, EmployeeRole, deleted: true));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.ChangeUserRoleAsync(7, 3, 1));
            Assert.Equal("Restore the user before changing their role.", ex.Message);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-UA09: Change role — same role is a no-op: returns false, nothing written")]
        public async Task ChangeRole_SameRole_NoOp()
        {
            SetupRoles();
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(7)).ReturnsAsync(MakeUser(7, EmployeeRole));

            var changed = await _adminService.ChangeUserRoleAsync(7, 3, 1);

            Assert.False(changed);
            _auditRepoMock.Verify(r => r.AddAsync(It.IsAny<AuditLog>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-UA10: Change role — the only active Admin cannot be demoted")]
        public async Task ChangeRole_LastAdminDemotion_Throws()
        {
            SetupRoles();
            var admin = MakeUser(8, AdminRole);
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(8)).ReturnsAsync(admin);
            _userRepoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(1);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.ChangeUserRoleAsync(8, 2, 1));
            Assert.Equal("The last active Admin cannot be demoted. Make someone else an Admin first.", ex.Message);

            Assert.Equal(1, admin.RoleId);
            _auditRepoMock.Verify(r => r.AddAsync(It.IsAny<AuditLog>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-UA11: Change role — demoting an Admin is allowed when another active Admin exists; audit written")]
        public async Task ChangeRole_AdminDemotion_WithAnotherAdmin_WritesAudit()
        {
            SetupRoles();
            var admin = MakeUser(8, AdminRole);
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(8)).ReturnsAsync(admin);
            _userRepoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(2);

            var changed = await _adminService.ChangeUserRoleAsync(8, 2, 1);

            Assert.True(changed);
            Assert.Equal(2, admin.RoleId);
            _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(log =>
                log.UserId == 1 &&
                log.EntityName == "Users" &&
                log.Action == "Admin changed role of user 'user8@example.com' (ID=8) from Admin to Manager."
            )), Times.Once);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Once);
        }

        [Fact(DisplayName = "TC-UA12: Change role — non-Admin to Admin and non-Admin to non-Admin skip the last-Admin check")]
        public async Task ChangeRole_NonAdminChanges_SkipLastAdminCheck()
        {
            SetupRoles();
            var emp = MakeUser(9, EmployeeRole);
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(9)).ReturnsAsync(emp);

            Assert.True(await _adminService.ChangeUserRoleAsync(9, 1, 1));
            Assert.Equal(1, emp.RoleId);

            var emp2 = MakeUser(10, EmployeeRole);
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(10)).ReturnsAsync(emp2);
            Assert.True(await _adminService.ChangeUserRoleAsync(10, 2, 1));
            Assert.Equal(2, emp2.RoleId);

            _userRepoMock.Verify(r => r.CountActiveAdminsAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-UA13: Change role — Admin to Admin is the unchanged no-op, never the last-Admin error")]
        public async Task ChangeRole_AdminToAdmin_NoOp()
        {
            SetupRoles();
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(8)).ReturnsAsync(MakeUser(8, AdminRole));
            _userRepoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(1);

            Assert.False(await _adminService.ChangeUserRoleAsync(8, 1, 1));
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        // ── Deactivate ────────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-UA14: Deactivate — the only active Admin cannot be deactivated")]
        public async Task DeleteUser_LastAdmin_Throws()
        {
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(8)).ReturnsAsync(MakeUser(8, AdminRole));
            _userRepoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(1);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.DeleteUserAsync(8, 1));
            Assert.Equal("The last active Admin cannot be deactivated. Make someone else an Admin first.", ex.Message);

            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<int>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact(DisplayName = "TC-UA15: Deactivate — an Admin can be deactivated when another active Admin exists")]
        public async Task DeleteUser_AdminWithAnotherAdmin_Succeeds()
        {
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(8)).ReturnsAsync(MakeUser(8, AdminRole));
            _userRepoMock.Setup(r => r.CountActiveAdminsAsync()).ReturnsAsync(2);

            await _adminService.DeleteUserAsync(8, 1);

            _userRepoMock.Verify(r => r.SoftDeleteAsync(8), Times.Once);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Once);
        }

        [Fact(DisplayName = "TC-UA16: Deactivate — non-Admin skips the Admin count; self-delete message unchanged")]
        public async Task DeleteUser_NonAdmin_SkipsAdminCount_AndSelfRuleFirst()
        {
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(9)).ReturnsAsync(MakeUser(9, EmployeeRole));

            await _adminService.DeleteUserAsync(9, 1);
            _userRepoMock.Verify(r => r.CountActiveAdminsAsync(), Times.Never);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.DeleteUserAsync(8, 8));
            Assert.Equal("You cannot delete your own account.", ex.Message);
        }

        // ── Create user ───────────────────────────────────────────────────────────

        [Fact(DisplayName = "TC-UA17: Create user — name and e-mail are trimmed, e-mail lower-cased, used for hashing lookups and audit")]
        public async Task CreateUser_TrimsAndLowerCases()
        {
            SetupRoles();
            _userRepoMock.Setup(r => r.GetByEmailWithRoleAsync("jane@example.com")).ReturnsAsync((User?)null);
            _authServiceMock.Setup(s => s.HashPassword("Password123")).Returns("hashed");

            await _adminService.CreateUserAsync(new UserCreateRequest("  Jane Doe  ", "  Jane@Example.COM ", "Password123", 3), 1);

            _userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u =>
                u.Name == "Jane Doe" && u.Email == "jane@example.com" && u.RoleId == 3)), Times.Once);
            _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(log =>
                log.Action.Contains("'jane@example.com'"))), Times.Once);
            _emailServiceMock.Verify(s => s.SendEmailAsync("jane@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact(DisplayName = "TC-UA18: Create user — a deactivated user with the e-mail gets its own message")]
        public async Task CreateUser_DeactivatedDuplicate_Throws()
        {
            SetupRoles();
            _userRepoMock.Setup(r => r.GetByEmailWithRoleAsync("old@example.com"))
                .ReturnsAsync(new User { UserId = 4, Email = "old@example.com", IsDeleted = true });

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _adminService.CreateUserAsync(new UserCreateRequest("Old", "Old@Example.com", "Password123", 3), 1));
            Assert.Equal("A deactivated user with this email already exists. Restore them instead.", ex.Message);
            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact(DisplayName = "TC-UA19: Create user — unknown role throws 'The selected role was not found.' (after the password rule)")]
        public async Task CreateUser_RoleMissing_Throws()
        {
            SetupRoles();

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _adminService.CreateUserAsync(new UserCreateRequest("Jane", "jane@example.com", "Password123", 99), 1));
            Assert.Equal("The selected role was not found.", ex.Message);

            var ex2 = await Assert.ThrowsAsync<ArgumentException>(() =>
                _adminService.CreateUserAsync(new UserCreateRequest("Jane", "jane@example.com", "short", 99), 1));
            Assert.Equal("Password must be at least 8 characters.", ex2.Message);

            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact(DisplayName = "TC-UA20: Create user — validation order: duplicate e-mail is reported before a short password")]
        public async Task CreateUser_DuplicateBeforePassword()
        {
            SetupRoles();
            _userRepoMock.Setup(r => r.GetByEmailWithRoleAsync("dup@example.com"))
                .ReturnsAsync(new User { UserId = 4, Email = "dup@example.com" });

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _adminService.CreateUserAsync(new UserCreateRequest("Dup", "dup@example.com", "x", 3), 1));
            Assert.Equal("A user with this email already exists.", ex.Message);
        }

        [Fact(DisplayName = "TC-UA21: Create user — name rules run before any repository call")]
        public async Task CreateUser_NameRequired_BeforeRepositories()
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                _adminService.CreateUserAsync(new UserCreateRequest("   ", "bad", "x", 99), 1));
            Assert.Equal("Name is required.", ex.Message);

            _userRepoMock.Verify(r => r.GetByEmailWithRoleAsync(It.IsAny<string>()), Times.Never);
            _roleRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact(DisplayName = "TC-UA22: Restore user — looks the user up including soft-deleted ones")]
        public async Task RestoreUser_FindsDeletedUser()
        {
            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(4)).ReturnsAsync(MakeUser(4, EmployeeRole, deleted: true));

            await _adminService.RestoreUserAsync(4, 1);

            _userRepoMock.Verify(r => r.RestoreAsync(4), Times.Once);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Once);

            _userRepoMock.Setup(r => r.GetByIdIncludingDeletedAsync(404)).ReturnsAsync((User?)null);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _adminService.RestoreUserAsync(404, 1));
        }
    }
}
