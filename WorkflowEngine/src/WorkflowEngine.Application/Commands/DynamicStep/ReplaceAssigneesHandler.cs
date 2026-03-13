using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.DynamicStep;

public class ReplaceAssigneesHandler : IRequestHandler<ReplaceAssigneesCommand, ApprovalStepDto>
{
    private readonly IUnitOfWork _uow;
    public ReplaceAssigneesHandler(IUnitOfWork uow) { _uow = uow; }

    public async Task<ApprovalStepDto> Handle(ReplaceAssigneesCommand cmd, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetWithStepsAsync(cmd.InstanceId, ct)
            ?? throw new NotFoundException("ProcessInstance", cmd.InstanceId);

        var step = instance.ApprovalSteps.FirstOrDefault(s => s.Id == cmd.StepId)
            ?? throw new NotFoundException("ApprovalStep", cmd.StepId);

        if (step.Status != StepStatus.Pending)
            throw new StepNotModifiableException(cmd.StepId);

        await _uow.BeginTransactionAsync(ct);

        step.Assignees = cmd.Assignees.Select(a => new StepAssignee(a.UserId)).ToList();
        _uow.ApprovalSteps.Update(step);
        await _uow.CommitAsync(ct);

        return new ApprovalStepDto
        {
            Id = step.Id,
            StepIndex = step.StepIndex,
            Type = step.Type.ToString(),
            Status = step.Status.ToString(),
            Source = step.Source.ToString(),
            Assignees = step.Assignees.Select(a => new AssigneeDto { UserId = a.UserId }).ToList(),
        };
    }
}
