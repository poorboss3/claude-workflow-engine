using MediatR;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Events;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.Approval;

public class ReturnTaskHandler : IRequestHandler<ReturnTaskCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;

    public ReturnTaskHandler(IUnitOfWork uow, IMediator mediator) { _uow = uow; _mediator = mediator; }

    public async Task Handle(ReturnTaskCommand cmd, CancellationToken ct)
    {
        var task = await _uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new NotFoundException("WorkflowTask", cmd.TaskId);

        if (task.AssigneeId != cmd.CurrentUserId)
            throw new PermissionDeniedException("You are not the assignee of this task");
        if (task.Status != Domain.Enums.TaskStatus.Pending)
            throw new WorkflowException("Task is not in pending status");

        await _uow.BeginTransactionAsync(ct);

        task.Status = Domain.Enums.TaskStatus.Returned;
        task.Action = Domain.Enums.TaskAction.Return;
        task.Comment = cmd.Comment;
        task.CompletedAt = DateTime.UtcNow;
        _uow.Tasks.Update(task);

        var instance = await _uow.ProcessInstances.GetWithStepsAsync(task.InstanceId, ct)!;
        var currentStep = instance!.ApprovalSteps.First(s => s.Id == task.StepId);

        ApprovalStep targetStep;
        if (cmd.TargetStepId.HasValue)
        {
            targetStep = instance.ApprovalSteps.FirstOrDefault(s => s.Id == cmd.TargetStepId.Value)
                ?? throw new NotFoundException("ApprovalStep", cmd.TargetStepId.Value);
        }
        else
        {
            targetStep = instance.ApprovalSteps.OrderBy(s => s.StepIndex).First();
        }

        // Reset steps between target and current
        var stepsToReset = instance.ApprovalSteps
            .Where(s => s.StepIndex >= targetStep.StepIndex && s.StepIndex < currentStep.StepIndex)
            .ToList();

        foreach (var step in stepsToReset)
        {
            step.Status = StepStatus.Pending;
            step.CompletedAt = null;
            _uow.ApprovalSteps.Update(step);
        }

        // Skip sibling tasks
        var siblingTasks = await _uow.Tasks.GetActiveTasksByStepAsync(currentStep.Id, ct);
        foreach (var t in siblingTasks.Where(t => t.Id != task.Id && t.Status == Domain.Enums.TaskStatus.Pending))
        {
            t.Status = Domain.Enums.TaskStatus.Skipped;
            _uow.Tasks.Update(t);
        }

        targetStep.Status = StepStatus.Active;
        instance.CurrentStepIndex = targetStep.StepIndex;
        _uow.ApprovalSteps.Update(targetStep);
        _uow.ProcessInstances.Update(instance);

        var newTasks = targetStep.Assignees.Select(a => new WorkflowTask
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            StepId = targetStep.Id,
            AssigneeId = a.UserId,
            OriginalAssigneeId = a.IsDelegated ? a.OriginalUserId : null,
            IsDelegated = a.IsDelegated,
            Status = Domain.Enums.TaskStatus.Pending,
            IsUrgent = instance.IsUrgent,
        }).ToList();
        foreach (var t in newTasks) _uow.Tasks.Add(t);

        await _uow.CommitAsync(ct);
        await _mediator.Publish(new TaskReturnedEvent(instance.Id, targetStep.Id, newTasks), ct);
    }
}
