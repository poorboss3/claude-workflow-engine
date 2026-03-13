using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Infrastructure.Persistence.Configurations;

public class ProxyConfigEntityConfig : IEntityTypeConfiguration<ProxyConfig>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public void Configure(EntityTypeBuilder<ProxyConfig> builder)
    {
        builder.ToTable("proxy_configs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.PrincipalId).HasColumnName("principal_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.AgentId).HasColumnName("agent_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.AllowedProcessTypes)
               .HasColumnName("allowed_process_types")
               .HasColumnType("jsonb")
               .HasConversion(
                   v => JsonSerializer.Serialize(v, JsonOpts),
                   v => JsonSerializer.Deserialize<List<string>>(v, JsonOpts) ?? new())
               .IsRequired();
        builder.Property(x => x.ValidFrom).HasColumnName("valid_from").IsRequired();
        builder.Property(x => x.ValidTo).HasColumnName("valid_to").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => new { x.AgentId, x.IsActive });
        builder.HasIndex(x => new { x.PrincipalId, x.IsActive });
    }
}
