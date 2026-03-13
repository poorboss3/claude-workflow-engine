namespace WorkflowEngine.Application.DTOs;

public class ProxyConfigDto
{
    public Guid Id { get; set; }
    public string PrincipalId { get; set; } = "";
    public string AgentId { get; set; } = "";
    public List<string> AllowedProcessTypes { get; set; } = [];
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; }
}

public class PrincipalDto
{
    public string UserId { get; set; } = "";
    public List<string> AllowedProcessTypes { get; set; } = [];
    public DateTime ValidTo { get; set; }
}

public class DelegationConfigDto
{
    public Guid Id { get; set; }
    public string DelegatorId { get; set; } = "";
    public string DelegateeId { get; set; } = "";
    public List<string> AllowedProcessTypes { get; set; } = [];
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}
