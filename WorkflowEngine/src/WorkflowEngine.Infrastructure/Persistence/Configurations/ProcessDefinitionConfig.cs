using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ProcessDefinitionConfig : IEntityTypeConfiguration<ProcessDefinition>
{
    public void Configure(EntityTypeBuilder<ProcessDefinition> builder)
    {
        builder.ToTable("process_definitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProcessType).HasColumnName("process_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Version).HasColumnName("version").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(x => x.NodeTemplatesJson).HasColumnName("node_templates").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RuleSetId).HasColumnName("rule_set_id");
        builder.Property(x => x.ApproverResolverUrl).HasColumnName("approver_resolver_url").HasMaxLength(500);
        builder.Property(x => x.PermissionValidatorUrl).HasColumnName("permission_validator_url").HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => new { x.ProcessType, x.Version }).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
