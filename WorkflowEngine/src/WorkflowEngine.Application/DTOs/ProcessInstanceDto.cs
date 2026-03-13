namespace WorkflowEngine.Application.DTOs;

public class ProcessInstanceDto
{
    public Guid Id { get; set; }
    public Guid DefinitionId { get; set; }
    public string ProcessType { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string BusinessKey { get; set; } = "";
    public string SubmittedBy { get; set; } = "";
    public string? OnBehalfOf { get; set; }
    public string Status { get; set; } = "";
    public bool IsUrgent { get; set; }
    public decimal? CurrentStepIndex { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<ApprovalStepDto> Steps { get; set; } = [];
}

public class ApprovalStepDto
{
    public Guid Id { get; set; }
    public decimal StepIndex { get; set; }
    public string Type { get; set; } = "";
    public List<AssigneeDto> Assignees { get; set; } = [];
    public string? JointSignPolicy { get; set; }
    public string Status { get; set; } = "";
    public string Source { get; set; } = "";
    public string? AddedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AssigneeDto
{
    public string UserId { get; set; } = "";
    public string? OriginalUserId { get; set; }
    public bool IsDelegated { get; set; }
}
