using DevFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DevFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects", t => t.HasCheckConstraint("CK_Project_Key", "\"Key\" ~ '^[A-Z][A-Z0-9]{1,9}$'"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.WorkspaceId, x.Id });
        b.Property(x => x.Key).HasMaxLength(10).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.HasIndex(x => new { x.WorkspaceId, x.Key }).IsUnique();
        b.HasOne<Workspace>().WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
    }
}
