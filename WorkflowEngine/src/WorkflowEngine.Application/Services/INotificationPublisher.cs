using WorkflowEngine.Application.Messages;

namespace WorkflowEngine.Application.Services;

public interface IWorkflowNotificationPublisher
{
    Task PublishAsync(WorkflowNotification notification, CancellationToken ct = default);
}
