namespace WorkflowEngine.Domain.Entities;

public class ApprovalRule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Priority { get; set; }
    public Guid? ProcessDefinitionId { get; set; }
    public string ConditionsJson { get; set; } = "[]";
    public string ConditionLogic { get; set; } = "AND";
    public string ResultJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
