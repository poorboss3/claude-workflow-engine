using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Commands.Proxy;

public class CreateProxyConfigHandler : IRequestHandler<CreateProxyConfigCommand, ProxyConfigDto>
{
    private readonly IUnitOfWork _uow;
    public CreateProxyConfigHandler(IUnitOfWork uow) { _uow = uow; }

    public async Task<ProxyConfigDto> Handle(CreateProxyConfigCommand cmd, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        var config = new ProxyConfig
        {
            Id = Guid.NewGuid(),
            PrincipalId = cmd.PrincipalId,
            AgentId = cmd.AgentId,
            AllowedProcessTypes = cmd.AllowedProcessTypes,
            ValidFrom = cmd.ValidFrom,
            ValidTo = cmd.ValidTo,
            IsActive = true,
        };
        _uow.ProxyConfigs.Add(config);
        await _uow.CommitAsync(ct);

        return new ProxyConfigDto
        {
            Id = config.Id,
            PrincipalId = config.PrincipalId,
            AgentId = config.AgentId,
            AllowedProcessTypes = config.AllowedProcessTypes,
            ValidFrom = config.ValidFrom,
            ValidTo = config.ValidTo,
            IsActive = config.IsActive,
        };
    }
}
