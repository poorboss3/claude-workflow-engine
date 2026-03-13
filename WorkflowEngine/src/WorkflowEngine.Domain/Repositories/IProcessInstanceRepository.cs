using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IProcessInstanceRepository : IRepository<ProcessInstance>
{
    Task<ProcessInstance?> GetByBusinessKeyAsync(string businessKey, CancellationToken ct = default);
    Task<ProcessInstance?> GetWithStepsAsync(Guid id, CancellationToken ct = default);
    Task<ProcessInstance?> GetWithStepsAndTasksAsync(Guid id, CancellationToken ct = default);
}
