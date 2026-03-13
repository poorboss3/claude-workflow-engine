using MassTransit;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Messages;

namespace WorkflowEngine.Infrastructure.Messaging.Consumers;

public class NotificationConsumer : IConsumer<WorkflowNotification>
{
    private readonly ILogger<NotificationConsumer> _logger;

    public NotificationConsumer(ILogger<NotificationConsumer> logger)
        => _logger = logger;

    public async Task Consume(ConsumeContext<WorkflowNotification> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Processing notification: EventType={EventType}, Recipient={Recipient}, Instance={Instance}",
            msg.EventType, msg.RecipientId, msg.InstanceId);

        // 站内信
        await SendInboxAsync(msg);

        // 加急：短信
        if (msg.IsUrgent)
            await SendSmsAsync(msg);
    }

    private Task SendInboxAsync(WorkflowNotification msg)
    {
        _logger.LogDebug("InboxMessage -> {Recipient}: [{EventType}] {ProcessName} ({BusinessKey})",
            msg.RecipientId, msg.EventType, msg.ProcessName, msg.BusinessKey);
        return Task.CompletedTask;
    }

    private Task SendSmsAsync(WorkflowNotification msg)
    {
        _logger.LogInformation("SMS [URGENT] -> {Recipient}: {ProcessName} needs your approval",
            msg.RecipientId, msg.ProcessName);
        return Task.CompletedTask;
    }
}
