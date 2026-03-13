using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ProcessInstanceRepository : BaseRepository<ProcessInstance>, IProcessInstanceRepository
{
    public ProcessInstanceRepository(WorkflowDbContext ctx) : base(ctx) { }

    public async Task<ProcessInstance?> GetByBusinessKeyAsync(string businessKey, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(i => i.BusinessKey == businessKey, ct);

    public async Task<ProcessInstance?> GetWithStepsAsync(Guid id, CancellationToken ct = default)
        => await DbSet.Include(i => i.ApprovalSteps.OrderBy(s => s.StepIndex))
                      .FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<ProcessInstance?> GetWithStepsAndTasksAsync(Guid id, CancellationToken ct = default)
        => await DbSet
                      .Include(i => i.ApprovalSteps.OrderBy(s => s.StepIndex))
                          .ThenInclude(s => s.Tasks)
                      .FirstOrDefaultAsync(i => i.Id == id, ct);
}
