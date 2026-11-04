using DevFlow.Infrastructure.Persistence;
using DevFlow.Application.Identity;
using DevFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace DevFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DevFlow");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:DevFlow using User Secrets or an environment variable.");

        services.AddDbContext<DevFlowDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUserProfiles, UserProfiles>();
        services.AddScoped<IResourceAccess, ResourceAccessService>();
        return services;
    }
}
