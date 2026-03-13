using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ApproverModificationRepository : BaseRepository<ApproverListModification>, IApproverModificationRepository
{
    public ApproverModificationRepository(WorkflowDbContext ctx) : base(ctx) { }

    public async Task<List<ApproverListModification>> GetByInstanceAsync(Guid instanceId, CancellationToken ct = default)
        => await DbSet.Where(m => m.InstanceId == instanceId).OrderByDescending(m => m.ModifiedAt).ToListAsync(ct);
}
