namespace WorkflowEngine.Domain.Entities;

public class ProxyConfig
{
    public Guid Id { get; set; }
    public string PrincipalId { get; set; } = "";   // 被代理人 B
    public string AgentId { get; set; } = "";         // 代理人 A
    public List<string> AllowedProcessTypes { get; set; } = [];
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
