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
        var dtos = result.Items.Select(t => new PendingTaskDto
        {
            TaskId            = t.Id,
            InstanceId        = t.InstanceId,
            ProcessName       = "",   // 需要联查，此处简化留空
            BusinessKey       = "",
            InitiatorId       = "",
            IsUrgent          = t.IsUrgent,
            IsDelegated       = t.IsDelegated,
            OriginalAssigneeId = t.OriginalAssigneeId,
            PendingSince      = t.CreatedAt,
            FormSummaryJson   = "{}",
            StepType          = t.Step?.Type.ToString() ?? "",
        }).ToList();
        return new PagedResult<PendingTaskDto>(dtos, result.TotalCount, result.Page, result.PageSize);
    }
}
