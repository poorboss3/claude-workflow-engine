using WorkflowEngine.Domain.Entities;
using WorkflowEngine.Domain.Enums;

namespace WorkflowEngine.Domain.Repositories;

public interface IApprovalStepRepository : IRepository<ApprovalStep>
{
    Task<List<ApprovalStep>> GetByInstanceAsync(Guid instanceId, CancellationToken ct = default);
    Task<List<ApprovalStep>> GetByStatusAsync(Guid instanceId, StepStatus[] statuses, CancellationToken ct = default);
}
