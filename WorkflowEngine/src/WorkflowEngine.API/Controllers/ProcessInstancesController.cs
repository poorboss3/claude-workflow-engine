using MediatR;
using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.API.Models.Requests;
using WorkflowEngine.API.Models.Responses;
using WorkflowEngine.Application.Commands.Submission;
using WorkflowEngine.Application.Services;

namespace WorkflowEngine.API.Controllers;

[ApiController]
[Route("api/v1/process-instances")]
public class ProcessInstancesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ProcessInstancesController(IMediator mediator, ICurrentUserService currentUser)
    { _mediator = mediator; _currentUser = currentUser; }

    [HttpPost("prepare")]
    public async Task<ApiResponse<PrepareSubmitResult>> Prepare(
        [FromBody] PrepareSubmitRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new PrepareSubmitCommand(
            req.ProcessType, req.FormData, _currentUser.UserId, req.OnBehalfOf), ct);
        return ApiResponse<PrepareSubmitResult>.Ok(result);
    }

    [HttpPost]
    public async Task<ApiResponse<object>> Submit(
        [FromBody] SubmitProcessRequest req, CancellationToken ct)
    {
        var confirmedSteps = req.ConfirmedSteps.Select(s => new ConfirmedStep(
            s.StepIndex, s.Type,
            s.Assignees.Select(a => new ConfirmedAssignee(a.UserId)).ToList(),
            s.JointSignPolicy)).ToList();

        var result = await _mediator.Send(new SubmitProcessCommand(
            req.ProcessType, req.BusinessKey, req.FormData,
            _currentUser.UserId, req.OnBehalfOf, confirmedSteps), ct);

        return ApiResponse<object>.Ok(result);
    }
}
