using WorkflowEngine.Domain.Exceptions;

namespace WorkflowEngine.Application.Services;

public interface IPermissionValidator
{
    Task<ValidatePermissionsResult> ValidateAsync(ValidatePermissionsContext context, CancellationToken ct = default);
}

public record ValidatePermissionsContext(
    string ProcessType,
    Dictionary<string, object> FormData,
    string SubmittedBy,
    List<ResolvedStep> OriginalSteps,
    List<ConfirmedStep> FinalSteps,
    bool IsModified,
    string? CallbackUrl);

public record ValidatePermissionsResult(bool Passed, List<PermissionFailItem> FailedItems, string Message);

public record ConfirmedStep(
    decimal StepIndex,
    string Type,
    List<ConfirmedAssignee> Assignees,
    string? JointSignPolicy = null);

public record ConfirmedAssignee(
    string UserId,
    string? OriginalUserId = null,
    bool IsDelegated = false);
