using MediatR;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Events;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.Approval;

public class ApproveTaskHandler : IRequestHandler<ApproveTaskCommand>
{
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly IDistributedLockService _lockService;

    public ApproveTaskHandler(IUnitOfWork uow, IMediator mediator, IDistributedLockService lockService)
    {
        _uow = uow;
        _mediator = mediator;
        _lockService = lockService;
    }

    public async Task Handle(ApproveTaskCommand cmd, CancellationToken ct)
    {
        var task = await _uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new NotFoundException("WorkflowTask", cmd.TaskId);

        if (task.AssigneeId != cmd.CurrentUserId)
            throw new PermissionDeniedException("You are not the assignee of this task");
        if (task.Status != Domain.Enums.TaskStatus.Pending)
            throw new WorkflowException("Task is not in pending status");

        await using var stepLock = await _lockService.AcquireAsync($"step:{task.StepId}:advance", TimeSpan.FromSeconds(10), ct);
        if (stepLock == null)
            throw new ConcurrentConflictException("操作冲突，请稍后重试");

        await _uow.BeginTransactionAsync(ct);

        task.Status = Domain.Enums.TaskStatus.Completed;
        task.Action = TaskAction.Approve;
        task.Comment = cmd.Comment;
        task.CompletedAt = DateTime.UtcNow;
        _uow.Tasks.Update(task);

        var step = await _uow.ApprovalSteps.GetByIdAsync(task.StepId, ct)!;
        var stepTasks = await _uow.Tasks.GetActiveTasksByStepAsync(step!.Id, ct);

        if (CanAdvanceStep(step, stepTasks))
        {
            // Skip remaining pending tasks (ANY_ONE scenario)
            foreach (var t in stepTasks.Where(t => t.Status == Domain.Enums.TaskStatus.Pending && t.Id != task.Id))
            {
                t.Status = Domain.Enums.TaskStatus.Skipped;
                _uow.Tasks.Update(t);
            }
            step.Status = StepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;
            _uow.ApprovalSteps.Update(step);

            var instance = await _uow.ProcessInstances.GetWithStepsAsync(step.InstanceId, ct)!;
            await AdvanceToNextStepAsync(instance!, step, ct);
        }

        await _uow.CommitAsync(ct);
        await _mediator.Publish(new TaskCompletedEvent(task.Id, task.InstanceId), ct);
    }

    private static bool CanAdvanceStep(ApprovalStep step, List<WorkflowTask> tasks)
    {
        var completedCount = tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Completed);
        var totalCount = tasks.Count(t => t.Status != Domain.Enums.TaskStatus.Skipped);
        return step.Type switch
        {
            StepType.Approval => true,
            StepType.Notify => true,
            StepType.JointSign => step.JointSignPolicy switch
            {
                JointSignPolicy.AnyOne => completedCount >= 1,
                JointSignPolicy.Majority => completedCount > totalCount / 2.0,
                JointSignPolicy.AllPass => completedCount == totalCount,
                _ => false
            },
            _ => false
        };
    }

    private async Task AdvanceToNextStepAsync(ProcessInstance instance, ApprovalStep currentStep, CancellationToken ct)
    {
        var allSteps = await _uow.ApprovalSteps.GetByInstanceAsync(instance.Id, ct);
        var nextStep = allSteps
            .Where(s => s.StepIndex > currentStep.StepIndex && s.Status == StepStatus.Pending)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault();

        if (nextStep == null)
        {
            instance.Status = ProcessStatus.Completed;
            instance.CompletedAt = DateTime.UtcNow;
            _uow.ProcessInstances.Update(instance);
            await _mediator.Publish(new ProcessCompletedEvent(instance.Id), ct);
            return;
        }

        nextStep.Status = StepStatus.Active;
        instance.CurrentStepIndex = nextStep.StepIndex;
        _uow.ApprovalSteps.Update(nextStep);
        _uow.ProcessInstances.Update(instance);

        var newTasks = nextStep.Assignees.Select(a => new WorkflowTask
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            StepId = nextStep.Id,
            AssigneeId = a.UserId,
            OriginalAssigneeId = a.IsDelegated ? a.OriginalUserId : null,
            IsDelegated = a.IsDelegated,
            Status = Domain.Enums.TaskStatus.Pending,
            IsUrgent = instance.IsUrgent,
        }).ToList();

        foreach (var t in newTasks) _uow.Tasks.Add(t);
        await _mediator.Publish(new StepActivatedEvent(instance.Id, nextStep.Id, newTasks), ct);
    }
}
