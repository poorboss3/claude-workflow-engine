namespace WorkflowEngine.Application.Services;

public interface IDistributedLockService
{
    Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken ct = default);
}
