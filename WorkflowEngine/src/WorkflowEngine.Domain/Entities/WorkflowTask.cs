using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

public class WorkflowTask
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public Guid StepId { get; set; }
    public string AssigneeId { get; set; } = "";
    public string? OriginalAssigneeId { get; set; }
    public bool IsDelegated { get; set; }
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Pending;
    public bool IsUrgent { get; set; }
    public TaskAction? Action { get; set; }
    public string? Comment { get; set; }
    public int RowVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ProcessInstance? Instance { get; set; }
    public ApprovalStep? Step { get; set; }
}
