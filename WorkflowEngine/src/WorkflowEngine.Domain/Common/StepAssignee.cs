namespace WorkflowEngine.Domain.Common;

public class StepAssignee
{
    public string UserId { get; set; } = "";
    public string? OriginalUserId { get; set; }
    public bool IsDelegated { get; set; }

    public StepAssignee() { }
    public StepAssignee(string userId) { UserId = userId; }
    public StepAssignee(string userId, string originalUserId)
    {
        UserId = userId;
        OriginalUserId = originalUserId;
        IsDelegated = true;
    }
}
