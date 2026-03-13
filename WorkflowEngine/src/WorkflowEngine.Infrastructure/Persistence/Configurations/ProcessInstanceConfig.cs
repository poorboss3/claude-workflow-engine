using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ProcessInstanceConfig : IEntityTypeConfiguration<ProcessInstance>
{
    public void Configure(EntityTypeBuilder<ProcessInstance> builder)
    {
        builder.ToTable("process_instances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DefinitionId).HasColumnName("definition_id").IsRequired();
        builder.Property(x => x.DefinitionVersion).HasColumnName("definition_version").IsRequired();
        builder.Property(x => x.BusinessKey).HasColumnName("business_key").HasMaxLength(200).IsRequired();
        builder.Property(x => x.FormDataSnapshotJson).HasColumnName("form_data_snapshot").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.SubmittedBy).HasColumnName("submitted_by").HasMaxLength(100).IsRequired();
        builder.Property(x => x.OnBehalfOf).HasColumnName("on_behalf_of").HasMaxLength(100);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(x => x.IsUrgent).HasColumnName("is_urgent").IsRequired();
        builder.Property(x => x.CurrentStepIndex).HasColumnName("current_step_index").HasColumnType("decimal(10,4)");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(x => x.BusinessKey).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.SubmittedBy);

        builder.HasMany(x => x.ApprovalSteps)
               .WithOne(s => s.Instance)
               .HasForeignKey(s => s.InstanceId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(x => x.Definition);
    }
}
