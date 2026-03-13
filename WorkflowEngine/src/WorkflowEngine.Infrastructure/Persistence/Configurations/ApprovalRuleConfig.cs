using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ApprovalRuleConfig : IEntityTypeConfiguration<ApprovalRule>
{
    public void Configure(EntityTypeBuilder<ApprovalRule> builder)
    {
        builder.ToTable("approval_rules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Priority).HasColumnName("priority").IsRequired();
        builder.Property(x => x.ProcessDefinitionId).HasColumnName("process_definition_id");
        builder.Property(x => x.ConditionsJson).HasColumnName("conditions").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ConditionLogic).HasColumnName("condition_logic").HasMaxLength(10).IsRequired();
        builder.Property(x => x.ResultJson).HasColumnName("result").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => new { x.ProcessDefinitionId, x.IsActive });
    }
}
