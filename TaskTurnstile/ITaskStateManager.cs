namespace TaskTurnstile;

public interface ITaskStateManager
{
    /// <summary>Raw data query — returns true if a record exists, regardless of MaxRuntime. Use for observational purposes only.</summary>
    Task<bool> IsRunningAsync(string taskName, CancellationToken cancellationToken = default);

    /// <summary>Returns true if no record exists, or if a record exists but MaxRuntime has elapsed. Use as the gate for all start decisions.</summary>
    Task<bool> CanStartAsync(string taskName, CancellationToken cancellationToken = default);

    /// <summary>Waits until the task can start, then starts it, runs the work, and stops it. Throws OperationCanceledException if cancelled during wait.</summary>
    Task RunAsync(string taskName, Func<CancellationToken, Task> work, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default);

    /// <summary>If already running, returns false immediately. Otherwise starts the task, runs the work, stops it, and returns true.</summary>
    Task<bool> TryRunAsync(string taskName, Func<CancellationToken, Task> work, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default);

    /// <summary>If already running, returns TryRunResult.Skipped. Otherwise starts the task, runs the work, stops it, and returns TryRunResult.Ran(value).</summary>
    Task<TryRunResult<T>> TryRunAsync<T>(string taskName, Func<CancellationToken, Task<T>> work, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default);

    /// <summary>Blocks (polling) until the task can start, then marks it as started. Throws TimeoutException if maxWaitMs is exceeded.</summary>
    Task WaitAsync(string taskName, CancellationToken cancellationToken = default);

    /// <summary>Blocks (polling) until the task can start, then marks it as started. Throws TimeoutException if maxWaitMs is exceeded.</summary>
    Task WaitAsync(string taskName, int pollIntervalMs, int? maxWaitMs = null, CancellationToken cancellationToken = default);

    /// <summary>Marks the task as started. Returns false if already running.</summary>
    Task<bool> StartAsync(string taskName, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default);

    /// <summary>Marks the task as stopped. Always completes — ignores cancellation to ensure cleanup runs.</summary>
    Task<bool> TryStopAsync(string taskName);
}
