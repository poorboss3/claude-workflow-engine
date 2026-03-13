using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Entities;

public class ApprovalStep
{
    public Guid Id { get; set; }
    public Guid InstanceId { get; set; }
    public decimal StepIndex { get; set; }
    public StepType Type { get; set; }
    public List<StepAssignee> Assignees { get; set; } = [];
    public JointSignPolicy? JointSignPolicy { get; set; }
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public StepSource Source { get; set; } = StepSource.Original;
    public string? AddedByUserId { get; set; }
    public DateTime? AddedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ProcessInstance? Instance { get; set; }
    public List<WorkflowTask> Tasks { get; set; } = [];
}
