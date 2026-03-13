using MediatR;
using WorkflowEngine.Application.DTOs;

namespace WorkflowEngine.Application.Queries.Proxy;

public record GetMyPrincipalsQuery(string AgentId) : IRequest<List<PrincipalDto>>;
