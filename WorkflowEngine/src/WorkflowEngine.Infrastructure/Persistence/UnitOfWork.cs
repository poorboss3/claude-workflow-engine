using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WorkflowEngine.Domain.Repositories;
using WorkflowEngine.Infrastructure.Persistence.Repositories;

namespace WorkflowEngine.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly WorkflowDbContext _ctx;
    private IDbContextTransaction? _transaction;

    public IProcessDefinitionRepository ProcessDefinitions { get; }
    public IProcessInstanceRepository ProcessInstances { get; }
    public IApprovalStepRepository ApprovalSteps { get; }
    public ITaskRepository Tasks { get; }
    public IApprovalRuleRepository ApprovalRules { get; }
    public IProxyConfigRepository ProxyConfigs { get; }
    public IDelegationConfigRepository DelegationConfigs { get; }
    public IApproverModificationRepository ApproverModifications { get; }

    public UnitOfWork(WorkflowDbContext ctx)
    {
        _ctx = ctx;
        ProcessDefinitions = new ProcessDefinitionRepository(ctx);
        ProcessInstances = new ProcessInstanceRepository(ctx);
        ApprovalSteps = new ApprovalStepRepository(ctx);
        Tasks = new TaskRepository(ctx);
        ApprovalRules = new ApprovalRuleRepository(ctx);
        ProxyConfigs = new ProxyConfigRepository(ctx);
        DelegationConfigs = new DelegationConfigRepository(ctx);
        ApproverModifications = new ApproverModificationRepository(ctx);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _ctx.SaveChangesAsync(ct);

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_ctx.Database.IsInMemory()) return;
        _transaction = await _ctx.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _ctx.SaveChangesAsync(ct);
        if (_transaction != null) await _transaction.CommitAsync(ct);
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction != null) await _transaction.RollbackAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null) await _transaction.DisposeAsync();
        await _ctx.DisposeAsync();
    }
}
