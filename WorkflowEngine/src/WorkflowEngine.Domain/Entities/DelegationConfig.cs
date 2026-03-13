namespace WorkflowEngine.Domain.Entities;

public class DelegationConfig
{
    public Guid Id { get; set; }
    public string DelegatorId { get; set; } = "";     // 委托人（休假者）
    public string DelegateeId { get; set; } = "";     // 受托人
    public List<string> AllowedProcessTypes { get; set; } = [];
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
