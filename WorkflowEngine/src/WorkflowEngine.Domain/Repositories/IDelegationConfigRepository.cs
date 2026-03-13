using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IDelegationConfigRepository : IRepository<DelegationConfig>
{
    Task<List<DelegationConfig>> GetActiveByDelegatorAsync(string delegatorId, DateTime now, CancellationToken ct = default);
    Task<List<DelegationConfig>> GetActiveForUsersAsync(List<string> userIds, string processType, DateTime now, CancellationToken ct = default);
}
