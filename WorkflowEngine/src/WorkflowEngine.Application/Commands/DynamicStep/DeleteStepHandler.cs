using MediatR;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Exceptions;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.DynamicStep;

public class DeleteStepHandler : IRequestHandler<DeleteStepCommand>
{
    private readonly IUnitOfWork _uow;
    public DeleteStepHandler(IUnitOfWork uow) { _uow = uow; }

    public async Task Handle(DeleteStepCommand cmd, CancellationToken ct)
    {
        var instance = await _uow.ProcessInstances.GetWithStepsAsync(cmd.InstanceId, ct)
            ?? throw new NotFoundException("ProcessInstance", cmd.InstanceId);

        var step = instance.ApprovalSteps.FirstOrDefault(s => s.Id == cmd.StepId)
            ?? throw new NotFoundException("ApprovalStep", cmd.StepId);

        if (step.Status != StepStatus.Pending)
            throw new StepNotModifiableException(cmd.StepId);

        await _uow.BeginTransactionAsync(ct);
        _uow.ApprovalSteps.Remove(step);
        await _uow.CommitAsync(ct);
    }
}
