namespace WorkflowEngine.Domain.Exceptions;

public class WorkflowException : Exception
{
    public string Code { get; }
    public WorkflowException(string message, string code = "WORKFLOW_ERROR") : base(message) { Code = code; }
}

public class NotFoundException : WorkflowException
{
    public NotFoundException(string entity, object id)
        : base($"{entity} '{id}' not found", "NOT_FOUND") { }
}

public class PermissionDeniedException : WorkflowException
{
    public PermissionDeniedException(string message) : base(message, "PERMISSION_DENIED") { }
}

public class PermissionValidationFailedException : WorkflowException
{
    public List<PermissionFailItem> FailedItems { get; }
    public PermissionValidationFailedException(List<PermissionFailItem> items, string message)
        : base(message, "PERMISSION_VALIDATION_FAILED")
    {
        FailedItems = items;
    }
}

public class ConcurrentConflictException : WorkflowException
{
    public ConcurrentConflictException(string message) : base(message, "CONCURRENT_CONFLICT") { }
}

public class StepNotModifiableException : WorkflowException
{
    public StepNotModifiableException(Guid stepId)
        : base($"Step '{stepId}' cannot be modified (already active or completed)", "STEP_NOT_MODIFIABLE") { }
}

public record PermissionFailItem(decimal StepIndex, string AssigneeId, string Reason);
