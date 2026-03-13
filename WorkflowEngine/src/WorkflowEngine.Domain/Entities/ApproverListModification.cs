namespace WorkflowEngine.Domain.Entities;

public class ApproverListModification
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public string ModifiedBy { get; set; } = "";
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string OriginalStepsJson { get; set; } = "[]";
    public string FinalStepsJson { get; set; } = "[]";
    public string DiffSummaryJson { get; set; } = "[]";

    // Navigation
    public ProcessInstance? Instance { get; set; }
}
