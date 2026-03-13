using MediatR;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Events;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.Approval;

public class RejectTaskHandler : IRequestHandler<RejectTaskCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;

    public RejectTaskHandler(IUnitOfWork uow, IMediator mediator) { _uow = uow; _mediator = mediator; }

    public async Task Handle(RejectTaskCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.Comment))
            throw new WorkflowException("驳回必须填写原因", "VALIDATION_ERROR");

        var task = await _uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new NotFoundException("WorkflowTask", cmd.TaskId);

        if (task.AssigneeId != cmd.CurrentUserId)
            throw new PermissionDeniedException("You are not the assignee of this task");
        if (task.Status != Domain.Enums.TaskStatus.Pending)
            throw new WorkflowException("Task is not in pending status");

        await _uow.BeginTransactionAsync(ct);

        task.Status = Domain.Enums.TaskStatus.Rejected;
        task.Action = Domain.Enums.TaskAction.Reject;
        task.Comment = cmd.Comment;
        task.CompletedAt = DateTime.UtcNow;
        _uow.Tasks.Update(task);

        var instance = await _uow.ProcessInstances.GetByIdAsync(task.InstanceId, ct)!;
        var activeSteps = await _uow.ApprovalSteps.GetByStatusAsync(
            instance!.Id, [StepStatus.Pending, StepStatus.Active], ct);

        foreach (var step in activeSteps)
        {
            step.Status = StepStatus.Skipped;
            _uow.ApprovalSteps.Update(step);
            var pendingTasks = await _uow.Tasks.GetActiveTasksByStepAsync(step.Id, ct);
            foreach (var t in pendingTasks.Where(t => t.Id != task.Id))
            {
                t.Status = Domain.Enums.TaskStatus.Skipped;
                _uow.Tasks.Update(t);
            }
        }

        instance.Status = ProcessStatus.Rejected;
        instance.CompletedAt = DateTime.UtcNow;
        _uow.ProcessInstances.Update(instance);

        await _uow.CommitAsync(ct);
        await _mediator.Publish(new ProcessRejectedEvent(instance.Id, task.AssigneeId, cmd.Comment), ct);
    }
}
