using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ApprovalStepConfig : IEntityTypeConfiguration<ApprovalStep>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.ToTable("approval_steps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstanceId).HasColumnName("instance_id").IsRequired();
        builder.Property(x => x.StepIndex).HasColumnName("step_index").HasColumnType("decimal(10,4)").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().IsRequired();
        builder.Property(x => x.Assignees)
               .HasColumnName("assignees")
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonOpts),
                   v => JsonSerializer.Deserialize<List<StepAssignee>>(v, JsonOpts) ?? new())
               .IsRequired();
        builder.Property(x => x.JointSignPolicy).HasColumnName("joint_sign_policy").HasConversion<string?>();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasConversion<string>().IsRequired();
        builder.Property(x => x.AddedByUserId).HasColumnName("added_by_user_id").HasMaxLength(100);
        builder.Property(x => x.AddedAt).HasColumnName("added_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(x => new { x.InstanceId, x.StepIndex }).IsUnique();
        builder.HasIndex(x => new { x.InstanceId, x.Status });

        builder.HasMany(x => x.Tasks)
               .WithOne(t => t.Step)
               .HasForeignKey(t => t.StepId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
