using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowEngine.Application.Messages;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Events;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.EventHandlers;

public class ProcessSubmittedEventHandler : INotificationHandler<ProcessSubmittedEvent>
{
    private readonly IWorkflowNotificationPublisher _publisher;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProcessSubmittedEventHandler> _logger;

    public ProcessSubmittedEventHandler(IWorkflowNotificationPublisher publisher, IUnitOfWork uow, ILogger<ProcessSubmittedEventHandler> logger)
    { _publisher = publisher; _uow = uow; _logger = logger; }

    public async Task Handle(ProcessSubmittedEvent evt, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetByIdAsync(evt.InstanceId, ct);
        if (instance == null) return;
        var definition = await _uow.ProcessDefinitions.GetByIdAsync(instance.DefinitionId, ct);
        if (definition == null) return;

        foreach (var task in evt.NewTasks)
        {
            await _publisher.PublishAsync(new WorkflowNotification
            {
                InstanceId = evt.InstanceId,
                EventType = "task_assigned",
                RecipientId = task.AssigneeId,
                ProcessName = definition.Name,
                BusinessKey = instance.BusinessKey,
                IsUrgent = task.IsUrgent,
                ExtraData = new Dictionary<string, object> { ["isDelegated"] = task.IsDelegated },
            }, ct);
        }
    }
}

public class ProcessRejectedEventHandler : INotificationHandler<ProcessRejectedEvent>
{
    private readonly IWorkflowNotificationPublisher _publisher;
    private readonly IUnitOfWork _uow;

    public ProcessRejectedEventHandler(IWorkflowNotificationPublisher publisher, IUnitOfWork uow)
    { _publisher = publisher; _uow = uow; }

    public async Task Handle(ProcessRejectedEvent evt, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetByIdAsync(evt.InstanceId, ct);
        if (instance == null) return;
        var definition = await _uow.ProcessDefinitions.GetByIdAsync(instance.DefinitionId, ct);
        var initiator = instance.OnBehalfOf ?? instance.SubmittedBy;

        await _publisher.PublishAsync(new WorkflowNotification
        {
            InstanceId = evt.InstanceId,
            EventType = "process_rejected",
            RecipientId = initiator,
            ProcessName = definition?.Name ?? "",
            BusinessKey = instance.BusinessKey,
            Message = evt.RejectReason,
        }, ct);
    }
}

public class ProcessCompletedEventHandler : INotificationHandler<ProcessCompletedEvent>
{
    private readonly IWorkflowNotificationPublisher _publisher;
    private readonly IUnitOfWork _uow;

    public ProcessCompletedEventHandler(IWorkflowNotificationPublisher publisher, IUnitOfWork uow)
    { _publisher = publisher; _uow = uow; }

    public async Task Handle(ProcessCompletedEvent evt, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetByIdAsync(evt.InstanceId, ct);
        if (instance == null) return;
        var definition = await _uow.ProcessDefinitions.GetByIdAsync(instance.DefinitionId, ct);
        var initiator = instance.OnBehalfOf ?? instance.SubmittedBy;

        await _publisher.PublishAsync(new WorkflowNotification
        {
            InstanceId = evt.InstanceId,
            EventType = "process_completed",
            RecipientId = initiator,
            ProcessName = definition?.Name ?? "",
            BusinessKey = instance.BusinessKey,
        }, ct);
    }
}

public class StepActivatedEventHandler : INotificationHandler<StepActivatedEvent>
{
    private readonly IWorkflowNotificationPublisher _publisher;
    private readonly IUnitOfWork _uow;

    public StepActivatedEventHandler(IWorkflowNotificationPublisher publisher, IUnitOfWork uow)
    { _publisher = publisher; _uow = uow; }

    public async Task Handle(StepActivatedEvent evt, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetByIdAsync(evt.InstanceId, ct);
        if (instance == null) return;
        var definition = await _uow.ProcessDefinitions.GetByIdAsync(instance.DefinitionId, ct);

        foreach (var task in evt.NewTasks)
        {
            await _publisher.PublishAsync(new WorkflowNotification
            {
                InstanceId = evt.InstanceId,
                EventType = "task_assigned",
                RecipientId = task.AssigneeId,
                ProcessName = definition?.Name ?? "",
                BusinessKey = instance.BusinessKey,
                IsUrgent = task.IsUrgent,
            }, ct);
        }
    }
}
