using DevFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DevFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProjectMembershipConfiguration : IEntityTypeConfiguration<ProjectMembership>
{
    public void Configure(EntityTypeBuilder<ProjectMembership> b)
    {
        b.ToTable("ProjectMemberships", t => t.HasCheckConstraint("CK_ProjectMembership_Role", "\"Role\" IN ('Manager', 'Member', 'Viewer')"));
        b.HasKey(x => new { x.ProjectId, x.UserId });
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
        // Both constraints share WorkspaceId, preventing cross-workspace membership.
        b.HasOne<Project>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.ProjectId })
            .HasPrincipalKey(x => new { x.WorkspaceId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkspaceMembership>().WithMany().HasForeignKey(x => new { x.WorkspaceId, x.UserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
