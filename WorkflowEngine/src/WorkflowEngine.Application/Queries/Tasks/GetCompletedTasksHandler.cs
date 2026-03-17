using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Queries.Tasks;

public class GetCompletedTasksHandler : IRequestHandler<GetCompletedTasksQuery, PagedResult<CompletedTaskDto>>
{
    private readonly IUnitOfWork _uow;
    public GetCompletedTasksHandler(IUnitOfWork uow) { _uow = uow; }

    public async Task<PagedResult<CompletedTaskDto>> Handle(GetCompletedTasksQuery query, CancellationToken ct)
    {
        var result = await _uow.Tasks.GetCompletedTasksAsync(query.UserId, query.Page, query.PageSize, ct);

        var instanceIds = result.Items.Select(t => t.InstanceId).Distinct();
        var instances = new Dictionary<Guid, WorkflowEngine.Domain.Entities.ProcessInstance>();
        foreach (var id in instanceIds)
        {
            var inst = await _uow.ProcessInstances.GetByIdAsync(id, ct);
            if (inst != null) instances[id] = inst;
        }

        var defIds = instances.Values.Select(i => i.DefinitionId).Distinct();
        var defs = new Dictionary<Guid, WorkflowEngine.Domain.Entities.ProcessDefinition>();
        foreach (var id in defIds)
        {
            var def = await _uow.ProcessDefinitions.GetByIdAsync(id, ct);
            if (def != null) defs[id] = def;
        }

        var dtos = result.Items.Select(t =>
        {
            instances.TryGetValue(t.InstanceId, out var inst);
            var def = inst != null && defs.TryGetValue(inst.DefinitionId, out var d) ? d : null;
            return new CompletedTaskDto
            {
                TaskId             = t.Id,
                InstanceId         = t.InstanceId,
                ProcessName        = def?.Name ?? "",
                BusinessKey        = inst?.BusinessKey ?? "",
                Action             = t.Action?.ToString(),
                Comment            = t.Comment,
                IsDelegated        = t.IsDelegated,
                OriginalAssigneeId = t.OriginalAssigneeId,
                ProcessStatus      = inst?.Status.ToString() ?? "",
                CompletedAt        = t.CompletedAt,
            };
        }).ToList();
        return new PagedResult<CompletedTaskDto>(dtos, result.TotalCount, result.Page, result.PageSize);
    }
}
