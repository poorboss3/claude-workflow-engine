namespace WorkflowEngine.Application.Services;

public interface IApproverResolver
{
    Task<ResolveApproversResult> ResolveAsync(ResolveApproversContext context, CancellationToken ct = default);
}

public record ResolveApproversContext(
    string ProcessType,
    Dictionary<string, object> FormData,
    string SubmittedBy,
    string? OnBehalfOf,
    string? CallbackUrl);

public record ResolveApproversResult(List<ResolvedStep> Steps, Dictionary<string, object>? Metadata);

public record ResolvedStep(
    decimal StepIndex,
    string Type,
    List<ResolvedAssignee> Assignees,
    string? JointSignPolicy);

public record ResolvedAssignee(string UserId);
