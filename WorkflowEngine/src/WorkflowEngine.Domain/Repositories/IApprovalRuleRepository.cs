using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IApprovalRuleRepository : IRepository<ApprovalRule>
{
    Task<List<ApprovalRule>> GetByProcessTypeAsync(Guid? processDefinitionId, bool includeGlobal = true, CancellationToken ct = default);
}
