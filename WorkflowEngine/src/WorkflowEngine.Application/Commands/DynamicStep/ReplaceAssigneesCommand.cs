using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Application.Commands.DynamicStep;

public record ReplaceAssigneesCommand(
    Guid InstanceId,
    Guid StepId,
    List<ConfirmedAssignee> Assignees,
    string OperatorId) : IRequest<ApprovalStepDto>;
