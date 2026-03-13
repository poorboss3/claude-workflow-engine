using WorkflowEngine.Application.Messages;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Tests.Fakes;

public class FakeNotificationPublisher : IWorkflowNotificationPublisher
{
    public List<WorkflowNotification> Published { get; } = [];

    public Task PublishAsync(WorkflowNotification notification, CancellationToken ct = default)
    {
        Published.Add(notification);
        return Task.CompletedTask;
    }
}
