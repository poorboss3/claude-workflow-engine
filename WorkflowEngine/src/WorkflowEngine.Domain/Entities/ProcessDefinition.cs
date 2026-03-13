using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

public class ProcessDefinition
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ProcessType { get; set; } = "";
    public int Version { get; set; } = 1;
    public DefinitionStatus Status { get; set; } = DefinitionStatus.Draft;
    public string NodeTemplatesJson { get; set; } = "[]";
    public Guid? RuleSetId { get; set; }
    public string? ApproverResolverUrl { get; set; }
    public string? PermissionValidatorUrl { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
