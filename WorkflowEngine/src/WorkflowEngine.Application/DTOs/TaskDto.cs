namespace WorkflowEngine.Application.DTOs;

public class PendingTaskDto
{
    public Guid TaskId { get; set; }
    public Guid InstanceId { get; set; }
    public string ProcessName { get; set; } = "";
    public string BusinessKey { get; set; } = "";
    public string InitiatorId { get; set; } = "";
    public bool IsUrgent { get; set; }
    public bool IsDelegated { get; set; }
    public string? OriginalAssigneeId { get; set; }
    public DateTime PendingSince { get; set; }
    public string FormSummaryJson { get; set; } = "{}";
    public string StepType { get; set; } = "";
}

public class CompletedTaskDto
{
    public Guid TaskId { get; set; }
    public Guid InstanceId { get; set; }
    public string ProcessName { get; set; } = "";
    public string BusinessKey { get; set; } = "";
    public string? Action { get; set; }
    public string? Comment { get; set; }
    public bool IsDelegated { get; set; }
    public string? OriginalAssigneeId { get; set; }
    public string ProcessStatus { get; set; } = "";
    public DateTime? CompletedAt { get; set; }
}

public class TaskDetailDto : PendingTaskDto
{
    public List<ApprovalStepDto> Steps { get; set; } = [];
    public decimal CurrentStepIndex { get; set; }
}
