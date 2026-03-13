using WorkflowEngine.Application.Services;

namespace WorkflowEngine.Tests.Fakes;

public class FakeApproverResolver : IApproverResolver
{
    public List<ResolvedStep> Steps { get; set; } =
    [
        new ResolvedStep(1m, "Approval", [new ResolvedAssignee("approver-1")], null)
    ];

    public Task<ResolveApproversResult> ResolveAsync(ResolveApproversContext ctx, CancellationToken ct = default)
        => Task.FromResult(new ResolveApproversResult(Steps, null));
}
