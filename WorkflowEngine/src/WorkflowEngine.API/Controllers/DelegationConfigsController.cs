using MediatR;
using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.API.Models.Requests;
using WorkflowEngine.API.Models.Responses;
using WorkflowEngine.Application.Commands.Delegation;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.API.Controllers;

[ApiController]
[Route("api/v1/delegation-configs")]
public class DelegationConfigsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public DelegationConfigsController(IMediator mediator, ICurrentUserService currentUser, IUnitOfWork uow)
    { _mediator = mediator; _currentUser = currentUser; _uow = uow; }

    [HttpPost]
    public async Task<ApiResponse<object>> Create([FromBody] CreateDelegationRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateDelegationCommand(
            req.DelegatorId, req.DelegateeId, req.AllowedProcessTypes,
            req.ValidFrom, req.ValidTo, req.Reason), ct);
        return ApiResponse<object>.Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        var config = await _uow.DelegationConfigs.GetByIdAsync(id, ct);
        if (config == null) return NotFound();
        _uow.DelegationConfigs.Remove(config);
        await _uow.CommitAsync(ct);
        return NoContent();
    }
}
