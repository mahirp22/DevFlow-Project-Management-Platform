using DevFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DevFlow.Infrastructure.Persistence.Configurations;

internal sealed class WorkspaceMembershipConfiguration : IEntityTypeConfiguration<WorkspaceMembership>
{
    public void Configure(EntityTypeBuilder<WorkspaceMembership> b)
    {
        b.ToTable("WorkspaceMemberships", t => t.HasCheckConstraint("CK_WorkspaceMembership_Role", "\"Role\" IN ('Owner', 'Admin', 'Member')"));
        b.HasKey(x => new { x.WorkspaceId, x.UserId });
        b.Property(x => x.Role).HasConversion<string>().HasMaxLength(16);
        b.HasOne<Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
