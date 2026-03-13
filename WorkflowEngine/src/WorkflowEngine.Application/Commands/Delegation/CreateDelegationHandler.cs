using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.Delegation;

public class CreateDelegationHandler : IRequestHandler<CreateDelegationCommand, DelegationConfigDto>
{
    private readonly IUnitOfWork _uow;
    public CreateDelegationHandler(IUnitOfWork uow) { _uow = uow; }

    public async Task<DelegationConfigDto> Handle(CreateDelegationCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        var config = new DelegationConfig
        {
            Id = Guid.NewGuid(),
            DelegatorId = cmd.DelegatorId,
            DelegateeId = cmd.DelegateeId,
            AllowedProcessTypes = cmd.AllowedProcessTypes,
            ValidFrom = cmd.ValidFrom,
            ValidTo = cmd.ValidTo,
            IsActive = true,
            Reason = cmd.Reason,
        };
        _uow.DelegationConfigs.Add(config);
        await _uow.CommitAsync(ct);

        return new DelegationConfigDto
        {
            Id = config.Id,
            DelegatorId = config.DelegatorId,
            DelegateeId = config.DelegateeId,
            AllowedProcessTypes = config.AllowedProcessTypes,
            ValidFrom = config.ValidFrom,
            ValidTo = config.ValidTo,
            IsActive = config.IsActive,
            Reason = config.Reason,
        };
    }
}
