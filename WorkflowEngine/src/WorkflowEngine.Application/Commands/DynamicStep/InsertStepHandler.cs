using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.DynamicStep;

public class InsertStepHandler : IRequestHandler<InsertStepCommand, ApprovalStepDto>
{
    private readonly IUnitOfWork _uow;
    public InsertStepHandler(IUnitOfWork uow) { _uow = uow; }

    public async Task<ApprovalStepDto> Handle(InsertStepCommand cmd, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetWithStepsAsync(cmd.InstanceId, ct)
            ?? throw new NotFoundException("ProcessInstance", cmd.InstanceId);

        if (instance.Status != ProcessStatus.Running)
            throw new WorkflowException("Cannot modify a finished process");

        var afterStep = instance.ApprovalSteps.FirstOrDefault(s => s.Id == cmd.AfterStepId)
            ?? throw new NotFoundException("ApprovalStep", cmd.AfterStepId);

        if (afterStep.StepIndex < instance.CurrentStepIndex)
            throw new WorkflowException("不能在当前步骤之前插入新步骤");

        var nextStep = instance.ApprovalSteps
            .Where(s => s.StepIndex > afterStep.StepIndex)
            .OrderBy(s => s.StepIndex)
            .FirstOrDefault();

        decimal newIndex = nextStep == null
            ? afterStep.StepIndex + 1
            : (afterStep.StepIndex + nextStep.StepIndex) / 2;

        await _uow.BeginTransactionAsync(ct);

        var newStep = new ApprovalStep
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            StepIndex = newIndex,
            Type = Enum.Parse<StepType>(cmd.Type, true),
            Assignees = cmd.Assignees.Select(a => new StepAssignee(a.UserId)).ToList(),
            JointSignPolicy = cmd.JointSignPolicy != null ? Enum.Parse<JointSignPolicy>(cmd.JointSignPolicy, true) : null,
            Status = StepStatus.Pending,
            Source = StepSource.DynamicAdded,
            AddedByUserId = cmd.OperatorId,
            AddedAt = DateTime.UtcNow,
        };
        _uow.ApprovalSteps.Add(newStep);
        await _uow.CommitAsync(ct);

        return new ApprovalStepDto
        {
            Id = newStep.Id,
            StepIndex = newStep.StepIndex,
            Type = newStep.Type.ToString(),
            Status = newStep.Status.ToString(),
            Source = newStep.Source.ToString(),
            Assignees = newStep.Assignees.Select(a => new AssigneeDto { UserId = a.UserId }).ToList(),
        };
    }
}
