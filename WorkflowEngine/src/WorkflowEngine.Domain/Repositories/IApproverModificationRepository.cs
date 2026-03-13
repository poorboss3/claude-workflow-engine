using WorkflowEngine.Domain.Entities;

namespace WorkflowEngine.Domain.Repositories;

public interface IApproverModificationRepository : IRepository<ApproverListModification>
{
    Task<List<ApproverListModification>> GetByInstanceAsync(Guid instanceId, CancellationToken ct = default);
}
