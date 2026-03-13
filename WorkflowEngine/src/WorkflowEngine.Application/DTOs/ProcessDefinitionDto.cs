namespace WorkflowEngine.Application.DTOs;

public class ProcessDefinitionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ProcessType { get; set; } = "";
    public int Version { get; set; }
    public string Status { get; set; } = "";
    public string? ApproverResolverUrl { get; set; }
    public string? PermissionValidatorUrl { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
