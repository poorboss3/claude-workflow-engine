using MediatR;
using Microsoft.AspNetCore.Mvc;
using WorkflowEngine.API.Models.Responses;
using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.API.Controllers;

[ApiController]
[Route("api/v1/process-definitions")]
public class ProcessDefinitionsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ProcessDefinitionsController(IUnitOfWork uow, ICurrentUserService currentUser)
    { _uow = uow; _currentUser = currentUser; }

    [HttpGet]
    public async Task<ApiResponse<object>> GetList(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var items = await _uow.ProcessDefinitions.GetListAsync(status, page, pageSize, ct);
        var total = await _uow.ProcessDefinitions.CountAsync(status, ct);
        return ApiResponse<object>.Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<ProcessDefinition>> Get(Guid id, CancellationToken ct)
    {
        var item = await _uow.ProcessDefinitions.GetByIdAsync(id, ct);
        if (item == null) return ApiResponse<ProcessDefinition>.Fail("NOT_FOUND", $"ProcessDefinition {id} not found");
        return ApiResponse<ProcessDefinition>.Ok(item);
    }

    [HttpPost]
    public async Task<ApiResponse<ProcessDefinition>> Create([FromBody] CreateDefinitionRequest req, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            ProcessType = req.ProcessType,
            Version = 1,
            Status = DefinitionStatus.Draft,
            NodeTemplatesJson = req.NodeTemplatesJson ?? "[]",
            ApproverResolverUrl = req.ApproverResolverUrl,
            PermissionValidatorUrl = req.PermissionValidatorUrl,
            CreatedBy = _currentUser.UserId,
        };
        _uow.ProcessDefinitions.Add(definition);
        await _uow.CommitAsync(ct);
        return ApiResponse<ProcessDefinition>.Ok(definition);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ApiResponse<object>> Activate(Guid id, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        var definition = await _uow.ProcessDefinitions.GetByIdAsync(id, ct);
        if (definition == null) return ApiResponse<object>.Fail("NOT_FOUND", $"ProcessDefinition {id} not found");

        var current = await _uow.ProcessDefinitions.GetActiveByProcessTypeAsync(definition.ProcessType, ct);
        if (current != null && current.Id != id)
        {
            current.Status = DefinitionStatus.Archived;
            _uow.ProcessDefinitions.Update(current);
        }

        definition.Status = DefinitionStatus.Active;
        _uow.ProcessDefinitions.Update(definition);
        await _uow.CommitAsync(ct);
        return ApiResponse<object>.Ok(new { version = definition.Version });
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        var definition = await _uow.ProcessDefinitions.GetByIdAsync(id, ct);
        if (definition == null) return NotFound();
        definition.Status = DefinitionStatus.Archived;
        _uow.ProcessDefinitions.Update(definition);
        await _uow.CommitAsync(ct);
        return NoContent();
    }
}

public class CreateDefinitionRequest
{
    public string Name { get; set; } = "";
    public string ProcessType { get; set; } = "";
    public string? NodeTemplatesJson { get; set; }
    public string? ApproverResolverUrl { get; set; }
    public string? PermissionValidatorUrl { get; set; }
}
