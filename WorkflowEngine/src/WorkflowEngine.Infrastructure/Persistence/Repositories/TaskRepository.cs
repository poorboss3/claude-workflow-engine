using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Application.DTOs;
using WorkflowEngine.Domain.Common;
using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public class TaskRepository : BaseRepository<WorkflowTask>, ITaskRepository
{
    public TaskRepository(WorkflowDbContext ctx) : base(ctx) { }

    public async Task<PagedResult<WorkflowTask>> GetPendingTasksAsync(string userId, int page, int pageSize, CancellationToken ct = default)
    {
        var q = DbSet
            .Where(t => t.AssigneeId == userId && t.Status == Domain.Enums.TaskStatus.Pending)
            .Include(t => t.Step)
            .OrderByDescending(t => t.IsUrgent)
            .ThenBy(t => t.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<WorkflowTask>(items, total, page, pageSize);
    }

    public async Task<PagedResult<WorkflowTask>> GetCompletedTasksAsync(string userId, int page, int pageSize, CancellationToken ct = default)
    {
        var completed = new[] { Domain.Enums.TaskStatus.Completed, Domain.Enums.TaskStatus.Returned, Domain.Enums.TaskStatus.Rejected };
        var q = DbSet
            .Where(t => (t.AssigneeId == userId || t.OriginalAssigneeId == userId) && completed.Contains(t.Status))
            .Include(t => t.Step)
            .OrderByDescending(t => t.CompletedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<WorkflowTask>(items, total, page, pageSize);
    }

    public async Task<List<WorkflowTask>> GetActiveTasksByStepAsync(Guid stepId, CancellationToken ct = default)
        => await DbSet.Where(t => t.StepId == stepId && t.Status != Domain.Enums.TaskStatus.Skipped).ToListAsync(ct);
}
