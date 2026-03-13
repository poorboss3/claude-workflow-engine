using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

public class ProcessInstance
{
    public Guid Id { get; set; }
    public Guid DefinitionId { get; set; }
    public int DefinitionVersion { get; set; }
    public string BusinessKey { get; set; } = "";
    public string FormDataSnapshotJson { get; set; } = "{}";
    public string SubmittedBy { get; set; } = "";
    public string? OnBehalfOf { get; set; }
    public ProcessStatus Status { get; set; } = ProcessStatus.Running;
    public bool IsUrgent { get; set; }
    public decimal? CurrentStepIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ProcessDefinition? Definition { get; set; }
    public List<ApprovalStep> ApprovalSteps { get; set; } = [];
}
