using MediatR;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Application.Commands.Approval;

public record CountersignTaskCommand(
    Guid TaskId,
    string CurrentUserId,
    List<ConfirmedAssignee> Assignees,
    string? Comment) : IRequest<Guid>;
