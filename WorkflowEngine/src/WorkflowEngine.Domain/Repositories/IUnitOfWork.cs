namespace WorkflowEngine.Domain.Repositories;

public interface IUnitOfWork : IAsyncDisposable
{
    IProcessDefinitionRepository ProcessDefinitions { get; }
    IProcessInstanceRepository ProcessInstances { get; }
    IApprovalStepRepository ApprovalSteps { get; }
    ITaskRepository Tasks { get; }
    IApprovalRuleRepository ApprovalRules { get; }
    IProxyConfigRepository ProxyConfigs { get; }
    IDelegationConfigRepository DelegationConfigs { get; }
    IApproverModificationRepository ApproverModifications { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
