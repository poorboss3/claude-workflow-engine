using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Queries.Proxy;

public class GetMyPrincipalsHandler : IRequestHandler<GetMyPrincipalsQuery, List<PrincipalDto>>
{
    private readonly IUnitOfWork _uow;
    public GetMyPrincipalsHandler(IUnitOfWork uow) { _uow = uow; }

    public async Task<List<PrincipalDto>> Handle(GetMyPrincipalsQuery query, CancellationToken ct)
    {
        var configs = await _uow.ProxyConfigs.GetActiveByAgentAsync(query.AgentId, DateTime.UtcNow, ct);
        return configs.Select(c => new PrincipalDto
        {
            UserId = c.PrincipalId,
            AllowedProcessTypes = c.AllowedProcessTypes,
            ValidTo = c.ValidTo,
        }).ToList();
    }
}
