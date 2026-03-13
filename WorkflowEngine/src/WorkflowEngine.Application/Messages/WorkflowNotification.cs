namespace WorkflowEngine.Application.Messages;

public record WorkflowNotification
{
    public Guid InstanceId { get; init; }
    public string EventType { get; init; } = "";
    public string RecipientId { get; init; } = "";
    public string ProcessName { get; init; } = "";
    public string BusinessKey { get; init; } = "";
    public string? Message { get; init; }
    public bool IsUrgent { get; init; }
    public Dictionary<string, object> ExtraData { get; init; } = [];
}
