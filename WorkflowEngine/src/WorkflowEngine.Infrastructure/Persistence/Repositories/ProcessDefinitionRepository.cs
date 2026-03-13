using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class ProcessDefinitionRepository : BaseRepository<ProcessDefinition>, IProcessDefinitionRepository
{
    public ProcessDefinitionRepository(WorkflowDbContext ctx) : base(ctx) { }

    public async Task<ProcessDefinition?> GetActiveByProcessTypeAsync(string processType, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(d => d.ProcessType == processType && d.Status == DefinitionStatus.Active, ct);

    public async Task<List<ProcessDefinition>> GetListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = DbSet.AsQueryable();
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DefinitionStatus>(status, true, out var s))
            q = q.Where(d => d.Status == s);
        return await q.OrderByDescending(d => d.CreatedAt)
                      .Skip((page - 1) * pageSize).Take(pageSize)
                      .ToListAsync(ct);
    }

    public async Task<int> CountAsync(string? status, CancellationToken ct = default)
    {
        var q = DbSet.AsQueryable();
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DefinitionStatus>(status, true, out var s))
            q = q.Where(d => d.Status == s);
        return await q.CountAsync(ct);
    }
}
