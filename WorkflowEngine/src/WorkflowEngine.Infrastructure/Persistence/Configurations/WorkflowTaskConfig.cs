using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class WorkflowTaskConfig : IEntityTypeConfiguration<WorkflowTask>
{
    public void Configure(EntityTypeBuilder<WorkflowTask> builder)
    {
        builder.ToTable("tasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstanceId).HasColumnName("instance_id").IsRequired();
        builder.Property(x => x.StepId).HasColumnName("step_id").IsRequired();
        builder.Property(x => x.AssigneeId).HasColumnName("assignee_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.OriginalAssigneeId).HasColumnName("original_assignee_id").HasMaxLength(100);
        builder.Property(x => x.IsDelegated).HasColumnName("is_delegated").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(x => x.IsUrgent).HasColumnName("is_urgent").IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasConversion<string?>();
        builder.Property(x => x.Comment).HasColumnName("comment");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsRequired().IsConcurrencyToken();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(x => new { x.AssigneeId, x.Status });
        builder.HasIndex(x => x.InstanceId);
        builder.HasIndex(x => x.StepId);
        builder.HasIndex(x => x.OriginalAssigneeId);

        builder.Ignore(x => x.Instance);
    }
}
