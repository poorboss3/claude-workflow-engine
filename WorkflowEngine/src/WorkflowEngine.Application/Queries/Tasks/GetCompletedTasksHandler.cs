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
        var dtos = result.Items.Select(t => new CompletedTaskDto
        {
            TaskId             = t.Id,
            InstanceId         = t.InstanceId,
            ProcessName        = "",
            BusinessKey        = "",
            Action             = t.Action?.ToString(),
            Comment            = t.Comment,
            IsDelegated        = t.IsDelegated,
            OriginalAssigneeId = t.OriginalAssigneeId,
            ProcessStatus      = "",
            CompletedAt        = t.CompletedAt,
        }).ToList();
        return new PagedResult<CompletedTaskDto>(dtos, result.TotalCount, result.Page, result.PageSize);
    }
}
