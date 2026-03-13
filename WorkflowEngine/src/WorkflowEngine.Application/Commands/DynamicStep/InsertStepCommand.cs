using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Application.Commands.DynamicStep;

public record InsertStepCommand(
    Guid InstanceId,
    Guid AfterStepId,
    string Type,
    List<ConfirmedAssignee> Assignees,
    string? JointSignPolicy,
    string OperatorId) : IRequest<ApprovalStepDto>;
