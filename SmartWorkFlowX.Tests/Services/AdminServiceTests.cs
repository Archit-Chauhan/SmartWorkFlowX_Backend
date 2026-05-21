using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using Xunit;

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

        [Fact]
        public async Task GetAllUsersAsync_ShouldReturnAllUsersWithRoles()
        {
            // Arrange
            var role = new Role { RoleId = 1, RoleName = "Admin" };
            var users = new List<User>
            {
                new User { UserId = 1, Name = "Alice", Email = "alice@example.com", RoleId = 1, Role = role, CreatedAt = DateTime.UtcNow },
                new User { UserId = 2, Name = "Bob", Email = "bob@example.com", RoleId = 2, Role = null, CreatedAt = DateTime.UtcNow }
            };

            _userRepoMock.Setup(r => r.GetAllWithRolesAsync(It.IsAny<string?>()))
                .ReturnsAsync(users);

            // Act
            var result = await _adminService.GetAllUsersAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(1, root[0].GetProperty("UserId").GetInt32());
            Assert.Equal("Alice", root[0].GetProperty("Name").GetString());
            Assert.Equal("Admin", root[0].GetProperty("RoleName").GetString());

            Assert.Equal(2, root[1].GetProperty("UserId").GetInt32());
            Assert.Equal("bob@example.com", root[1].GetProperty("Email").GetString());
            Assert.Equal("No Role", root[1].GetProperty("RoleName").GetString());
        }

        [Fact]
        public async Task GetPaginatedUsersAsync_ShouldReturnCorrectPageAndTotal()
        {
            // Arrange
            var role = new Role { RoleId = 1, RoleName = "Employee" };
            var users = new List<User>
            {
                new User { UserId = 3, Name = "Carol", Email = "carol@example.com", RoleId = 1, Role = role, CreatedAt = DateTime.UtcNow }
            };

            _userRepoMock.Setup(r => r.GetPaginatedAsync(2, 10, It.IsAny<string?>()))
                .ReturnsAsync((users, 21));

            // Act
            var result = await _adminService.GetPaginatedUsersAsync(2, 10);

            // Assert
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

        [Fact]
        public async Task GetAllRolesAsync_ShouldReturnAllRoles()
        {
            // Arrange
            var roles = new List<Role>
            {
                new Role { RoleId = 1, RoleName = "Admin" },
                new Role { RoleId = 2, RoleName = "Employee" }
            };

            _roleRepoMock.Setup(r => r.GetAllAsync())
                .ReturnsAsync(roles);

            // Act
            var result = await _adminService.GetAllRolesAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);

            var json = System.Text.Json.JsonSerializer.Serialize(result);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(1, root[0].GetProperty("RoleId").GetInt32());
            Assert.Equal("Admin", root[0].GetProperty("RoleName").GetString());
            Assert.Equal(2, root[1].GetProperty("RoleId").GetInt32());
            Assert.Equal("Employee", root[1].GetProperty("RoleName").GetString());
        }

        [Fact]
        public async Task CreateUserAsync_ShouldThrowException_WhenEmailExists()
        {
            // Arrange
            var request = new UserCreateRequest("Duplicate User", "exists@example.com", "Password123", 3);

            _userRepoMock.Setup(r => r.EmailExistsAsync(request.Email))
                .ReturnsAsync(true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.CreateUserAsync(request, 1));
            Assert.Equal("A user with this email already exists.", ex.Message);

            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task CreateUserAsync_ShouldCreateUser_AndSendEmail_WhenValid()
        {
            // Arrange
            var request = new UserCreateRequest("New User", "new@example.com", "SecretPassword", 3);

            _userRepoMock.Setup(r => r.EmailExistsAsync(request.Email))
                .ReturnsAsync(false);

            _authServiceMock.Setup(s => s.HashPassword(request.Password))
                .Returns("hashed_secret_password");

            // Act
            var userId = await _adminService.CreateUserAsync(request, 1);

            // Assert
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
                request.Email,
                It.Is<string>(subject => subject.Contains("Welcome to SmartWorkFlowX")),
                It.Is<string>(body => body.Contains("Password:") && body.Contains("SecretPassword"))
            ), Times.Once);
        }

        [Fact]
        public async Task DeleteUserAsync_ShouldThrowException_WhenDeletingSelf()
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _adminService.DeleteUserAsync(1, 1));
            Assert.Equal("You cannot delete your own account.", ex.Message);

            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<int>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task DeleteUserAsync_ShouldThrowException_WhenUserNotFound()
        {
            // Arrange
            _userRepoMock.Setup(r => r.GetByIdAsync(99))
                .ReturnsAsync((User)null!);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _adminService.DeleteUserAsync(99, 1));
            Assert.Equal("User not found.", ex.Message);

            _userRepoMock.Verify(r => r.SoftDeleteAsync(It.IsAny<int>()), Times.Never);
            _userRepoMock.Verify(r => r.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task DeleteUserAsync_ShouldSoftDelete_AndWriteAuditLog_WhenValid()
        {
            // Arrange
            var user = new User { UserId = 2, Email = "delete_me@example.com" };
            _userRepoMock.Setup(r => r.GetByIdAsync(2))
                .ReturnsAsync(user);

            // Act
            await _adminService.DeleteUserAsync(2, 1);

            // Assert
            _userRepoMock.Verify(r => r.SoftDeleteAsync(2), Times.Once);

            _auditRepoMock.Verify(r => r.AddAsync(It.Is<AuditLog>(log =>
                log.UserId == 1 &&
                log.Action.Contains("Admin deleted user 'delete_me@example.com'") &&
                log.EntityName == "Users"
            )), Times.Once);

            _userRepoMock.Verify(r => r.SaveAsync(), Times.Once);
        }
    }
}
