using MediatR;
using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.API.Models.Requests;
using WorkflowEngine.API.Models.Responses;
using WorkflowEngine.Application.Commands.Approval;
using WorkflowEngine.Application.Commands.DynamicStep;
using WorkflowEngine.Application.Queries.Tasks;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.API.Controllers;

[ApiController]
[Route("api/v1/tasks")]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public TasksController(IMediator mediator, ICurrentUserService currentUser)
    { _mediator = mediator; _currentUser = currentUser; }

    [HttpGet("pending")]
    public async Task<ApiResponse<object>> GetPending(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPendingTasksQuery(_currentUser.UserId, page, pageSize), ct);
        return ApiResponse<object>.Ok(result);
    }

    [HttpGet("completed")]
    public async Task<ApiResponse<object>> GetCompleted(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCompletedTasksQuery(_currentUser.UserId, page, pageSize), ct);
        return ApiResponse<object>.Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveTaskRequest req, CancellationToken ct)
    {
        await _mediator.Send(new ApproveTaskCommand(id, _currentUser.UserId, req.Comment), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectTaskRequest req, CancellationToken ct)
    {
        await _mediator.Send(new RejectTaskCommand(id, _currentUser.UserId, req.Comment), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnTaskRequest req, CancellationToken ct)
    {
        await _mediator.Send(new ReturnTaskCommand(id, _currentUser.UserId, req.Comment, req.TargetStepId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/countersign")]
    public async Task<ApiResponse<object>> Countersign(Guid id, [FromBody] CountersignRequest req, CancellationToken ct)
    {
        var newStepId = await _mediator.Send(new CountersignTaskCommand(
            id, _currentUser.UserId,
            req.Assignees.Select(a => new ConfirmedAssignee(a.UserId)).ToList(),
            req.Comment), ct);
        return ApiResponse<object>.Ok(new { newStepId });
    }
}
