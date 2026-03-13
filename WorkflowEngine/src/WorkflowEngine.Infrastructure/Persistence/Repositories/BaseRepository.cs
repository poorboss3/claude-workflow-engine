using Microsoft.EntityFrameworkCore;
using WorkflowEngine.Domain.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence.Repositories;

public abstract class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly WorkflowDbContext Context;
    protected readonly DbSet<T> DbSet;

    protected BaseRepository(WorkflowDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet.FindAsync([id], ct);

    public virtual void Add(T entity) => DbSet.Add(entity);
    public virtual void Update(T entity) => DbSet.Update(entity);
    public virtual void Remove(T entity) => DbSet.Remove(entity);
}
