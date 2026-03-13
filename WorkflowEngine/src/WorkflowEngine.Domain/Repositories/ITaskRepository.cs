using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface ITaskRepository : IRepository<WorkflowTask>
{
    Task<PagedResult<WorkflowTask>> GetPendingTasksAsync(string userId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<WorkflowTask>> GetCompletedTasksAsync(string userId, int page, int pageSize, CancellationToken ct = default);
    Task<List<WorkflowTask>> GetActiveTasksByStepAsync(Guid stepId, CancellationToken ct = default);
}
