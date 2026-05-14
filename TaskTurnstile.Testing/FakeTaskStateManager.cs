using System.Collections.Concurrent;

namespace TaskTurnstile.Testing;

/// <summary>
/// A framework-agnostic in-memory test double for <see cref="ITaskStateManager"/>.
/// Work is executed inline — no distributed store, no polling, no background threads.
/// </summary>
/// <remarks>
/// <para>Works with any mocking framework or plain constructor injection:</para>
/// <code>
/// // AutoMocker (Moq)
/// Mocker.Use&lt;ITaskStateManager&gt;(new FakeTaskStateManager());
///
/// // NSubstitute / plain injection
/// var fake = new FakeTaskStateManager();
///
/// // Simulate "task already running" for testing the skip path
/// var fake = new FakeTaskStateManager();
/// fake.MarkRunning("my-job");
/// </code>
/// </remarks>
public sealed class FakeTaskStateManager : ITaskStateManager
{
    private readonly ConcurrentDictionary<string, byte> _running = new(StringComparer.Ordinal);

    /// <summary>
    /// How long <see cref="RunAsync"/> and <see cref="WaitAsync(string,CancellationToken)"/>
    /// will spin waiting for a task to become free before throwing <see cref="TimeoutException"/>.
    /// Defaults to 5 seconds. Set to <see cref="Timeout.InfiniteTimeSpan"/> to disable.
    /// </summary>
    public TimeSpan WaitTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Pre-marks <paramref name="taskName"/> as running. Use in test arrange steps to simulate
    /// a task that is already in progress, so that <see cref="TryRunAsync"/> returns
    /// <c>false</c> / <see cref="TryRunResult{T}.Skipped"/>.
    /// </summary>
    public void MarkRunning(string taskName) => _running.TryAdd(taskName, 0);

    /// <inheritdoc/>
    public Task<bool> IsRunningAsync(string taskName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_running.ContainsKey(taskName));

    /// <inheritdoc/>
    public Task<bool> CanStartAsync(string taskName, CancellationToken cancellationToken = default) =>
        Task.FromResult(!_running.ContainsKey(taskName));

    /// <inheritdoc/>
    public async Task<bool> TryRunAsync(string taskName, Func<CancellationToken, Task> work,
        TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        if (!_running.TryAdd(taskName, 0))
            return false;

        try
        {
            await work(cancellationToken);
            return true;
        }
        finally
        {
            _running.TryRemove(taskName, out _);
        }
    }

    /// <inheritdoc/>
    public async Task<TryRunResult<T>> TryRunAsync<T>(string taskName, Func<CancellationToken, Task<T>> work,
        TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        if (!_running.TryAdd(taskName, 0))
            return TryRunResult<T>.Skipped;

        try
        {
            var value = await work(cancellationToken);
            return TryRunResult<T>.Ran(value);
        }
        finally
        {
            _running.TryRemove(taskName, out _);
        }
    }

    /// <inheritdoc/>
    public async Task RunAsync(string taskName, Func<CancellationToken, Task> work,
        TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        await SpinUntilFreeAsync(taskName, cancellationToken);
        _running.TryAdd(taskName, 0);
        try
        {
            await work(cancellationToken);
        }
        finally
        {
            _running.TryRemove(taskName, out _);
        }
    }

    /// <inheritdoc/>
    public Task WaitAsync(string taskName, CancellationToken cancellationToken = default) =>
        WaitAsync(taskName, pollIntervalMs: 0, maxWaitMs: null, cancellationToken);

    /// <inheritdoc/>
    public async Task WaitAsync(string taskName, int pollIntervalMs, int? maxWaitMs = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTimeout = maxWaitMs.HasValue
            ? TimeSpan.FromMilliseconds(maxWaitMs.Value)
            : WaitTimeout;

        await SpinUntilFreeAsync(taskName, cancellationToken, effectiveTimeout);
        _running.TryAdd(taskName, 0);
    }

    /// <inheritdoc/>
    public Task<bool> StartAsync(string taskName, TimeSpan? maxRuntime = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_running.TryAdd(taskName, 0));

    /// <inheritdoc/>
    public Task<bool> TryStopAsync(string taskName)
    {
        _running.TryRemove(taskName, out _);
        return Task.FromResult(true);
    }

    private async Task SpinUntilFreeAsync(string taskName, CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? WaitTimeout;
        var deadline = effectiveTimeout == Timeout.InfiniteTimeSpan
            ? long.MaxValue
            : Environment.TickCount64 + (long)effectiveTimeout.TotalMilliseconds;

        while (_running.ContainsKey(taskName))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Environment.TickCount64 >= deadline)
                throw new TimeoutException(
                    $"FakeTaskStateManager: task '{taskName}' did not become free within {effectiveTimeout}. " +
                    $"Call TryStopAsync(\"{taskName}\") from another task to release it, or increase WaitTimeout.");

            await Task.Yield();
        }
    }
}
