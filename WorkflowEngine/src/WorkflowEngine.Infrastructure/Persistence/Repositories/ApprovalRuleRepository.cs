using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ApprovalRuleRepository : BaseRepository<ApprovalRule>, IApprovalRuleRepository
{
    public ApprovalRuleRepository(WorkflowDbContext ctx) : base(ctx) { }

    public async Task<List<ApprovalRule>> GetByProcessTypeAsync(Guid? processDefinitionId, bool includeGlobal = true, CancellationToken ct = default)
    {
        var q = DbSet.Where(r => r.IsActive &&
            (r.ProcessDefinitionId == processDefinitionId ||
             (includeGlobal && r.ProcessDefinitionId == null)));
        return await q.OrderByDescending(r => r.Priority).ToListAsync(ct);
    }
}
