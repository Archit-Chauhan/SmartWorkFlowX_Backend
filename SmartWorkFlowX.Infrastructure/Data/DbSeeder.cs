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
