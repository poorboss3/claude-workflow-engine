using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IProcessDefinitionRepository : IRepository<ProcessDefinition>
{
    Task<ProcessDefinition?> GetActiveByProcessTypeAsync(string processType, CancellationToken ct = default);
    Task<List<ProcessDefinition>> GetListAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAsync(string? status, CancellationToken ct = default);
}
