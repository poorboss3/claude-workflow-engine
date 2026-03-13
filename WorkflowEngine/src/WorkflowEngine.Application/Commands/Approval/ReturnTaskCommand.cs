using MediatR;

namespace WorkflowEngine.Application.Commands.Approval;

public record ReturnTaskCommand(Guid TaskId, string CurrentUserId, string Comment, Guid? TargetStepId = null) : IRequest;
