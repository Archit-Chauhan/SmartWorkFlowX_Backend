using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartWorkFlowX.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(SmartWorkflowXDbContext context, IAuthService authService)
        {
            // Seed Roles
            if (!await context.Roles.AnyAsync())
            {
                context.Roles.AddRange(
                    new Role { RoleName = "Admin" },
                    new Role { RoleName = "Manager" },
                    new Role { RoleName = "Employee" },
                    new Role { RoleName = "Auditor" }
                );
                await context.SaveChangesAsync();
            }

            // Seed Task Categories
            if (!await context.TaskCategories.AnyAsync())
            {
                context.TaskCategories.AddRange(
                    new TaskCategory { Name = "Bug Fix",       ColorHex = "#EF4444" },
                    new TaskCategory { Name = "Feature",       ColorHex = "#3B82F6" },
                    new TaskCategory { Name = "Meeting",       ColorHex = "#F59E0B" },
                    new TaskCategory { Name = "Code Review",   ColorHex = "#8B5CF6" },
                    new TaskCategory { Name = "Documentation", ColorHex = "#6B7280" },
                    new TaskCategory { Name = "Testing",       ColorHex = "#F97316" },
                    new TaskCategory { Name = "DevOps",        ColorHex = "#10B981" }
                );
                await context.SaveChangesAsync();
            }

            // Seed initial Admin user
            if (!await context.Users.AnyAsync(u => u.Email == "admin@smartworkflowx.com"))
            {
                var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
                if (adminRole != null)
                {
                    context.Users.Add(new User
                    {
                        Name = "System Administrator",
                        Email = "admin@smartworkflowx.com",
                        PasswordHash = authService.HashPassword("password123"),
                        RoleId = adminRole.RoleId,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    });
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
