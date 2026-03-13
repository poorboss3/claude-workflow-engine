using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ApprovalStepRepository : BaseRepository<ApprovalStep>, IApprovalStepRepository
{
    public ApprovalStepRepository(WorkflowDbContext ctx) : base(ctx) { }

    public async Task<List<ApprovalStep>> GetByInstanceAsync(Guid instanceId, CancellationToken ct = default)
        => await DbSet.Where(s => s.InstanceId == instanceId).OrderBy(s => s.StepIndex).ToListAsync(ct);

    public async Task<List<ApprovalStep>> GetByStatusAsync(Guid instanceId, StepStatus[] statuses, CancellationToken ct = default)
        => await DbSet.Where(s => s.InstanceId == instanceId && statuses.Contains(s.Status)).ToListAsync(ct);
}
