using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ProxyConfigRepository : BaseRepository<ProxyConfig>, IProxyConfigRepository
{
    public ProxyConfigRepository(WorkflowDbContext ctx) : base(ctx) { }

    public async Task<List<ProxyConfig>> GetActiveByAgentAsync(string agentId, DateTime now, CancellationToken ct = default)
        => await DbSet.Where(c => c.AgentId == agentId && c.IsActive && c.ValidFrom <= now && c.ValidTo >= now).ToListAsync(ct);

    public async Task<List<ProxyConfig>> FindActiveAsync(string agentId, string principalId, DateTime now, CancellationToken ct = default)
        => await DbSet.Where(c => c.AgentId == agentId && c.PrincipalId == principalId
                                  && c.IsActive && c.ValidFrom <= now && c.ValidTo >= now).ToListAsync(ct);
}
