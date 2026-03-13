using MassTransit;
using WorkflowEngine.Application.Messages;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Infrastructure.Messaging;

public class RabbitMqNotificationPublisher : IWorkflowNotificationPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public RabbitMqNotificationPublisher(IPublishEndpoint publishEndpoint)
        => _publishEndpoint = publishEndpoint;

    public Task PublishAsync(WorkflowNotification notification, CancellationToken ct = default)
        => _publishEndpoint.Publish(notification, ct);
}
