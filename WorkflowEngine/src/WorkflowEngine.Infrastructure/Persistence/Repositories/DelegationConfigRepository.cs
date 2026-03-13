using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class DelegationConfigRepository : BaseRepository<DelegationConfig>, IDelegationConfigRepository
{
    public DelegationConfigRepository(WorkflowDbContext ctx) : base(ctx) { }

    public async Task<List<DelegationConfig>> GetActiveByDelegatorAsync(string delegatorId, DateTime now, CancellationToken ct = default)
        => await DbSet.Where(d => d.DelegatorId == delegatorId && d.IsActive && d.ValidFrom <= now && d.ValidTo >= now).ToListAsync(ct);

    public async Task<List<DelegationConfig>> GetActiveForUsersAsync(List<string> userIds, string processType, DateTime now, CancellationToken ct = default)
        => await DbSet.Where(d => userIds.Contains(d.DelegatorId) && d.IsActive
                                  && d.ValidFrom <= now && d.ValidTo >= now
                                  && (!d.AllowedProcessTypes.Any() || d.AllowedProcessTypes.Contains(processType)))
                      .ToListAsync(ct);
}
