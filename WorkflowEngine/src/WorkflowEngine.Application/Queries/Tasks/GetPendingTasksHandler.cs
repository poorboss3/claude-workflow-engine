using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Application.Queries.Tasks;

public class GetPendingTasksHandler : IRequestHandler<GetPendingTasksQuery, PagedResult<PendingTaskDto>>
{
    private readonly IUnitOfWork _uow;
    public GetPendingTasksHandler(IUnitOfWork uow) { _uow = uow; }

    public async Task<PagedResult<PendingTaskDto>> Handle(GetPendingTasksQuery query, CancellationToken ct)
    {
        var result = await _uow.Tasks.GetPendingTasksAsync(query.UserId, query.Page, query.PageSize, ct);

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
            return new PendingTaskDto
            {
                TaskId             = t.Id,
                InstanceId         = t.InstanceId,
                ProcessName        = def?.Name ?? "",
                BusinessKey        = inst?.BusinessKey ?? "",
                InitiatorId        = inst?.SubmittedBy ?? "",
                IsUrgent           = t.IsUrgent,
                IsDelegated        = t.IsDelegated,
                OriginalAssigneeId = t.OriginalAssigneeId,
                PendingSince       = t.CreatedAt,
                FormSummaryJson    = "{}",
                StepType           = t.Step?.Type.ToString() ?? "",
            };
        }).ToList();
        return new PagedResult<PendingTaskDto>(dtos, result.TotalCount, result.Page, result.PageSize);
    }
}
