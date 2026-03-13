namespace WorkflowEngine.Application.Services;

public interface ICurrentUserService
{
    string UserId { get; }
    string? DisplayName { get; }
}
