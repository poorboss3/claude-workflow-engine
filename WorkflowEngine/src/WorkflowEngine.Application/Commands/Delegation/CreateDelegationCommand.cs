using MediatR;
using WorkflowEngine.Application.DTOs;

namespace WorkflowEngine.Application.Commands.Delegation;

public record CreateDelegationCommand(
    string DelegatorId,
    string DelegateeId,
    List<string> AllowedProcessTypes,
    DateTime ValidFrom,
    DateTime ValidTo,
    string? Reason) : IRequest<DelegationConfigDto>;
