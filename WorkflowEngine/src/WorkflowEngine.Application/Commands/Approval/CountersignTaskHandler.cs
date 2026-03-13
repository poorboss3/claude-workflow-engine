using MediatR;
using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.Approval;

public class CountersignTaskHandler : IRequestHandler<CountersignTaskCommand, Guid>
{
    private readonly IUnitOfWork _uow;

    public CountersignTaskHandler(IUnitOfWork uow) { _uow = uow; }

    public async Task<Guid> Handle(CountersignTaskCommand cmd, CancellationToken ct)
    {
        var task = await _uow.Tasks.GetByIdAsync(cmd.TaskId, ct)
            ?? throw new NotFoundException("WorkflowTask", cmd.TaskId);

        if (task.AssigneeId != cmd.CurrentUserId)
            throw new PermissionDeniedException("You are not the assignee of this task");
        if (task.Status != Domain.Enums.TaskStatus.Pending)
            throw new WorkflowException("Task is not in pending status");

        await _uow.BeginTransactionAsync(ct);

        var currentStep = await _uow.ApprovalSteps.GetByIdAsync(task.StepId, ct)!;
        var allSteps = await _uow.ApprovalSteps.GetByInstanceAsync(currentStep!.InstanceId, ct);

        var nextStep = allSteps
            .Where(s => s.StepIndex > currentStep.StepIndex && s.Status == StepStatus.Pending)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault();

        decimal newIndex = nextStep == null
            ? currentStep.StepIndex + 1
            : (currentStep.StepIndex + nextStep.StepIndex) / 2;

        if (allSteps.Any(s => s.StepIndex == newIndex))
            throw new WorkflowException("步骤索引冲突，请重新操作");

        var newStep = new ApprovalStep
        {
            Id = Guid.NewGuid(),
            InstanceId = currentStep.InstanceId,
            StepIndex = newIndex,
            Type = StepType.Approval,
            Assignees = cmd.Assignees.Select(a => new StepAssignee(a.UserId)).ToList(),
            Status = StepStatus.Pending,
            Source = StepSource.Countersign,
            AddedByUserId = cmd.CurrentUserId,
            AddedAt = DateTime.UtcNow,
        };
        _uow.ApprovalSteps.Add(newStep);

        task.Action = Domain.Enums.TaskAction.Countersign;
        task.Comment = cmd.Comment;
        _uow.Tasks.Update(task);

        await _uow.CommitAsync(ct);
        return newStep.Id;
    }
}
