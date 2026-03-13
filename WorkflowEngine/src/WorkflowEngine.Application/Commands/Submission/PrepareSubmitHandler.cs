using MediatR;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Domain.Exceptions;

namespace WorkflowEngine.Application.Commands.Submission;

public class PrepareSubmitHandler : IRequestHandler<PrepareSubmitCommand, PrepareSubmitResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IApproverResolver _resolver;

    public PrepareSubmitHandler(IUnitOfWork uow, IApproverResolver resolver)
    {
        _uow = uow;
        _resolver = resolver;
    }

    public async Task<PrepareSubmitResult> Handle(PrepareSubmitCommand cmd, CancellationToken ct)
    {
        var definition = await _uow.ProcessDefinitions.GetActiveByProcessTypeAsync(cmd.ProcessType, ct)
            ?? throw new NotFoundException("ProcessDefinition", cmd.ProcessType);

        var result = await _resolver.ResolveAsync(new ResolveApproversContext(
            cmd.ProcessType, cmd.FormData, cmd.SubmittedBy, cmd.OnBehalfOf,
            definition.ApproverResolverUrl), ct);

        return new PrepareSubmitResult(result.Steps, result.Metadata);
    }
}
