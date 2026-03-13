using MediatR;
using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.API.Models.Requests;
using WorkflowEngine.API.Models.Responses;
using WorkflowEngine.Application.Commands.Proxy;
using WorkflowEngine.Application.Queries.Proxy;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.API.Controllers;

[ApiController]
[Route("api/v1/proxy-configs")]
public class ProxyConfigsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public ProxyConfigsController(IMediator mediator, ICurrentUserService currentUser, IUnitOfWork uow)
    { _mediator = mediator; _currentUser = currentUser; _uow = uow; }

    [HttpGet("my-principals")]
    public async Task<ApiResponse<object>> GetMyPrincipals(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyPrincipalsQuery(_currentUser.UserId), ct);
        return ApiResponse<object>.Ok(result);
    }

    [HttpPost]
    public async Task<ApiResponse<object>> Create([FromBody] CreateProxyConfigRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateProxyConfigCommand(
            req.PrincipalId, req.AgentId, req.AllowedProcessTypes, req.ValidFrom, req.ValidTo), ct);
        return ApiResponse<object>.Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        var config = await _uow.ProxyConfigs.GetByIdAsync(id, ct);
        if (config == null) return NotFound();
        _uow.ProxyConfigs.Remove(config);
        await _uow.CommitAsync(ct);
        return NoContent();
    }
}
