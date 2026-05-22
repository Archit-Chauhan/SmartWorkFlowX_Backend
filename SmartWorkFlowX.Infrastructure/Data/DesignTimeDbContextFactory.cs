using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SmartWorkFlowX.Infrastructure.Data
{
    // Used exclusively by `dotnet ef` CLI tooling (migrations, database update).
    // Reads the connection string directly from the environment variable so CI
    // can inject it without starting the full ASP.NET Core host.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartWorkflowXDbContext>
    {
        public SmartWorkflowXDbContext CreateDbContext(string[] args)
        {
            // ASP.NET Core maps  ConnectionStrings__DefaultConnection  →  GetConnectionString("DefaultConnection")
            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Set ConnectionStrings__DefaultConnection before running EF tools.");

            var options = new DbContextOptionsBuilder<SmartWorkflowXDbContext>()
                .UseSqlServer(connectionString, sql =>
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null))
                .Options;

            return new SmartWorkflowXDbContext(options);
        }
    }
}
