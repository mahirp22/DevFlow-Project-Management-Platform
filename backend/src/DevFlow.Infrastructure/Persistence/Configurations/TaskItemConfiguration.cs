using DevFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DevFlow.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> b)
    {
        b.ToTable("Tasks", t =>
        {
            t.HasCheckConstraint("CK_Task_Number", "\"Number\" > 0");
            t.HasCheckConstraint("CK_Task_Status", "\"Status\" IN ('ToDo', 'InProgress', 'InReview', 'Completed')");
            t.HasCheckConstraint("CK_Task_Priority", "\"Priority\" IN ('Low', 'Medium', 'High', 'Urgent')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(10000);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.DueDate).HasColumnType("date");
        b.Property(x => x.Version).IsRowVersion(); // PostgreSQL xmin; never supplied by clients.
        b.HasIndex(x => new { x.ProjectId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.Status, x.Position });
        b.HasIndex(x => new { x.ProjectId, x.AssigneeUserId });
        b.HasIndex(x => new { x.ProjectId, x.DueDate });
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectMembership>().WithMany().HasForeignKey(x => new { x.ProjectId, x.AssigneeUserId })
            .OnDelete(DeleteBehavior.Restrict);
        // Reporters are historical actors, so leaving the project does not erase authorship.
        b.HasOne<UserProfile>().WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
