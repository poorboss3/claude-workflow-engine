using WorkflowEngine.Application.Services;
using WorkflowEngine.Domain.Exceptions;

namespace WorkflowEngine.Tests.Fakes;

public class FakePermissionValidator : IPermissionValidator
{
    public bool ShouldPass { get; set; } = true;
    public List<PermissionFailItem> FailItems { get; set; } = [];
    public string FailMessage { get; set; } = "Permission validation failed";

    public Task<ValidatePermissionsResult> ValidateAsync(ValidatePermissionsContext ctx, CancellationToken ct = default)
        => Task.FromResult(ShouldPass
            ? new ValidatePermissionsResult(true, [], "")
            : new ValidatePermissionsResult(false, FailItems, FailMessage));
}
