using MediatR;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Events;

public record ProcessSubmittedEvent(Guid InstanceId, List<WorkflowTask> NewTasks) : INotification;
public record TaskCompletedEvent(Guid TaskId, Guid InstanceId) : INotification;
public record ProcessCompletedEvent(Guid InstanceId) : INotification;
public record ProcessRejectedEvent(Guid InstanceId, string RejectedBy, string RejectReason) : INotification;
public record StepActivatedEvent(Guid InstanceId, Guid StepId, List<WorkflowTask> NewTasks) : INotification;
public record TaskReturnedEvent(Guid InstanceId, Guid TargetStepId, List<WorkflowTask> NewTasks) : INotification;
public record ProcessWithdrawnEvent(Guid InstanceId) : INotification;
public record UrgentMarkedEvent(Guid InstanceId) : INotification;
