namespace WorkflowEngine.API.Models.Requests;

public class CreateProxyConfigRequest
{
    public string PrincipalId { get; set; } = "";
    public string AgentId { get; set; } = "";
    public List<string> AllowedProcessTypes { get; set; } = [];
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}

public class CreateDelegationRequest
{
    public string DelegatorId { get; set; } = "";
    public string DelegateeId { get; set; } = "";
    public List<string> AllowedProcessTypes { get; set; } = [];
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public string? Reason { get; set; }
}
