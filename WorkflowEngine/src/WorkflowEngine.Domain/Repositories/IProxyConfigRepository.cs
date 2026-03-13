using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IProxyConfigRepository : IRepository<ProxyConfig>
{
    Task<List<ProxyConfig>> GetActiveByAgentAsync(string agentId, DateTime now, CancellationToken ct = default);
    Task<List<ProxyConfig>> FindActiveAsync(string agentId, string principalId, DateTime now, CancellationToken ct = default);
}
