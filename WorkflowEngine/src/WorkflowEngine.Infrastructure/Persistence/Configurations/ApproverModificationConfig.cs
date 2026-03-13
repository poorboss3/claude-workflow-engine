using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ApproverModificationConfig : IEntityTypeConfiguration<ApproverListModification>
{
    public void Configure(EntityTypeBuilder<ApproverListModification> builder)
    {
        builder.ToTable("approver_list_modifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.InstanceId).HasColumnName("instance_id").IsRequired();
        builder.Property(x => x.ModifiedBy).HasColumnName("modified_by").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ModifiedAt).HasColumnName("modified_at").IsRequired();
        builder.Property(x => x.OriginalStepsJson).HasColumnName("original_steps").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.FinalStepsJson).HasColumnName("final_steps").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DiffSummaryJson).HasColumnName("diff_summary").HasColumnType("jsonb").IsRequired();

        builder.HasIndex(x => new { x.InstanceId, x.ModifiedAt });
        builder.Ignore(x => x.Instance);
    }
}
