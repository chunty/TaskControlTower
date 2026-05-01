namespace ConcurrencyManager;

public interface ITaskStateStore
{
    Task<bool> IsRunningAsync(string taskName, CancellationToken cancellationToken = default);
    Task<bool> IsExpiredAsync(string taskName, CancellationToken cancellationToken = default);
    Task SetRunningAsync(string taskName, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default);
    Task SetStoppedAsync(string taskName, CancellationToken cancellationToken = default);
    Task CleanupAsync(CancellationToken cancellationToken = default);
}
