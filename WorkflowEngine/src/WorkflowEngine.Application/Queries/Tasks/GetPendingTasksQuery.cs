using MediatR;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Domain.Common;

namespace WorkflowEngine.Application.Queries.Tasks;

public record GetPendingTasksQuery(string UserId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<PendingTaskDto>>;
