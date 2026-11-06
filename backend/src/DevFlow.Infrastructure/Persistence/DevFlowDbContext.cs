using DevFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace DevFlow.Infrastructure.Persistence;

public sealed class DevFlowDbContext(DbContextOptions<DevFlowDbContext> options) : DbContext(options)
{
    public DbSet<TaskAttachment> Attachments => Set<TaskAttachment>();
    public DbSet<UserProfile> Users => Set<UserProfile>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMembership> WorkspaceMemberships => Set<WorkspaceMembership>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMembership> ProjectMemberships => Set<ProjectMembership>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskComment> Comments => Set<TaskComment>();
    public DbSet<TaskActivity> Activities => Set<TaskActivity>();
    public DbSet<ProjectInvitation> Invitations => Set<ProjectInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevFlowDbContext).Assembly);
    }
}
