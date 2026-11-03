using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace DevFlow.Infrastructure.Persistence;

// Migration tooling does not need to boot the web host or contact the database.
public sealed class DevFlowDbContextFactory : IDesignTimeDbContextFactory<DevFlowDbContext>
{
    public DevFlowDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DevFlow");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set ConnectionStrings__DevFlow before running EF migration commands.");
        return new DevFlowDbContext(new DbContextOptionsBuilder<DevFlowDbContext>()
            .UseNpgsql(connectionString).Options);
    }
}
