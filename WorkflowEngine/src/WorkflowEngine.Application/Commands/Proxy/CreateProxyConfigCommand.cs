using MediatR;
using WorkflowEngine.Application.DTOs;

namespace WorkflowEngine.Application.Commands.Proxy;

public record CreateProxyConfigCommand(
    string PrincipalId,
    string AgentId,
    List<string> AllowedProcessTypes,
    DateTime ValidFrom,
    DateTime ValidTo) : IRequest<ProxyConfigDto>;
