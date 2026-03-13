using MediatR;

namespace WorkflowEngine.Application.Commands.Approval;

public record ApproveTaskCommand(Guid TaskId, string CurrentUserId, string? Comment) : IRequest;
