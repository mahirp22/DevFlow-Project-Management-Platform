using DevFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DevFlow.Infrastructure.Persistence.Configurations;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<TaskAttachment>
{
    public void Configure(EntityTypeBuilder<TaskAttachment> b)
    {
        b.ToTable("TaskAttachments", t => t.HasCheckConstraint("CK_TaskAttachments_Size", "\"Size\" BETWEEN 1 AND 10485760"));
        b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(180).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.ObjectKey).HasMaxLength(300).IsRequired();
        b.Property(x => x.Checksum).HasMaxLength(44).IsRequired();
        b.HasIndex(x => x.ObjectKey).IsUnique();
        b.HasIndex(x => new { x.TaskId, x.CreatedAt });
        b.HasIndex(x => new { x.FinalizedAt, x.ExpiresAt, x.LastCleanedAt });
        b.HasOne<TaskItem>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.UploaderUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
