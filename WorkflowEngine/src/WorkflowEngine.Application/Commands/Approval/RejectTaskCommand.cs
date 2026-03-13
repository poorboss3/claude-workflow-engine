using MediatR;

namespace WorkflowEngine.Application.Commands.Approval;

public record RejectTaskCommand(Guid TaskId, string CurrentUserId, string Comment) : IRequest;
