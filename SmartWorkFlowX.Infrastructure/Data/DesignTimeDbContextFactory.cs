using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SmartWorkFlowX.Infrastructure.Data
{
    // Used exclusively by `dotnet ef` CLI tooling (migrations, database update).
    // Reads the connection string from the environment so CI can inject it
    // without starting the full ASP.NET Core host.
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SmartWorkflowXDbContext>
    {
        public SmartWorkflowXDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Set the ConnectionStrings__DefaultConnection environment variable before running EF tools.");

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
