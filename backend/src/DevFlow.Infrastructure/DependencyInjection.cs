using Amazon;
using Amazon.S3;
using DevFlow.Application.Attachments;
using DevFlow.Infrastructure.Attachments;
using DevFlow.Infrastructure.Persistence;
using DevFlow.Application.Identity;
using DevFlow.Infrastructure.Identity;
using DevFlow.Application.Projects;
using DevFlow.Infrastructure.Projects;
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
        services.AddScoped<AccessGuard>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IBoardService, BoardService>();
        services.AddScoped<IInvitationService, InvitationService>();
        var attachments = new AttachmentSettings { BucketName = configuration["Attachments:BucketName"] ?? "", Region = configuration["Attachments:Region"] ?? "" };
        attachments.Validate();
        services.AddSingleton(attachments);
        if (attachments.Configured)
        {
            services.AddSingleton<IAmazonS3>(_ =>
            {
                try { return new AmazonS3Client(new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(attachments.Region), Timeout = TimeSpan.FromSeconds(5), MaxErrorRetry = 1 }); }
                catch (Exception) { throw new AttachmentStorageUnavailableException(); }
            });
            services.AddSingleton<IAttachmentStorage, S3AttachmentStorage>();
        }
        else services.AddSingleton<IAttachmentStorage, DisabledAttachmentStorage>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        return services;
    }
}
