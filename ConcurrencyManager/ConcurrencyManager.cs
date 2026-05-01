using System.Diagnostics;
using AsyncKeyedLock;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ConcurrencyManager;

internal sealed class ConcurrencyManager(ITaskStateStore store, ConcurrencyManagerOptions options) : IConcurrencyManager
{
    private readonly AsyncKeyedLocker<string> _locker = new();
    private readonly IMemoryCache _localCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
    private const int DefaultPollIntervalMs = 250;

    public Task<bool> IsRunningAsync(string taskName, CancellationToken cancellationToken = default) =>
        store.IsRunningAsync(taskName, cancellationToken);

    public async Task<bool> CanStartAsync(string taskName, CancellationToken cancellationToken = default)
    {
        if (!await store.IsRunningAsync(taskName, cancellationToken))
            return true;

        return await store.IsExpiredAsync(taskName, cancellationToken);
    }

    public async Task RunAsync(string taskName, Func<CancellationToken, Task> work, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        await WaitAndStartAsync(taskName, maxRuntime ?? options.DefaultMaxRuntime, DefaultPollIntervalMs, null, cancellationToken);
        try
        {
            await work(cancellationToken);
        }
        finally
        {
            await TryStopAsync(taskName);
        }
    }

    public async Task<bool> TryRunAsync(string taskName, Func<CancellationToken, Task> work, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        if (!await StartAsync(taskName, maxRuntime, cancellationToken))
            return false;

        try
        {
            await work(cancellationToken);
            return true;
        }
        finally
        {
            await TryStopAsync(taskName);
        }
    }

    public async Task<TryRunResult<T>> TryRunAsync<T>(string taskName, Func<CancellationToken, Task<T>> work, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        if (!await StartAsync(taskName, maxRuntime, cancellationToken))
            return TryRunResult<T>.Skipped;

        try
        {
            var value = await work(cancellationToken);
            return TryRunResult<T>.Ran(value);
        }
        finally
        {
            await TryStopAsync(taskName);
        }
    }

    public Task WaitAsync(string taskName, CancellationToken cancellationToken = default) =>
        WaitAndStartAsync(taskName, options.DefaultMaxRuntime, DefaultPollIntervalMs, null, cancellationToken);

    public Task WaitAsync(string taskName, int pollIntervalMs, int? maxWaitMs = null, CancellationToken cancellationToken = default) =>
        WaitAndStartAsync(taskName, options.DefaultMaxRuntime, pollIntervalMs, maxWaitMs, cancellationToken);

    private async Task WaitAndStartAsync(string taskName, TimeSpan? maxRuntime, int pollIntervalMs, int? maxWaitMs, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Fast path: if this instance started the task and it hasn't expired locally,
            // skip the backing store query entirely.
            if (_localCache.TryGetValue(taskName, out _))
            {
                if (maxWaitMs.HasValue && stopwatch.ElapsedMilliseconds >= maxWaitMs.Value)
                    throw new TimeoutException($"Task '{taskName}' did not become available within {maxWaitMs.Value}ms.");

                await Task.Delay(pollIntervalMs, cancellationToken);
                continue;
            }

            if (await StartAsync(taskName, maxRuntime, cancellationToken))
                return;

            if (maxWaitMs.HasValue && stopwatch.ElapsedMilliseconds >= maxWaitMs.Value)
                throw new TimeoutException($"Task '{taskName}' did not become available within {maxWaitMs.Value}ms.");

            await Task.Delay(pollIntervalMs, cancellationToken);
        }
    }

    public async Task<bool> StartAsync(string taskName, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        using (await _locker.LockAsync(taskName, cancellationToken))
        {
            if (!await CanStartAsync(taskName, cancellationToken))
                return false;

            await store.SetRunningAsync(taskName, maxRuntime ?? options.DefaultMaxRuntime, cancellationToken);

            var effectiveRuntime = maxRuntime ?? options.DefaultMaxRuntime;
            if (effectiveRuntime.HasValue)
                _localCache.Set(taskName, true, absoluteExpirationRelativeToNow: effectiveRuntime.Value);
            else
                _localCache.Set(taskName, true);

            return true;
        }
    }

    public async Task<bool> TryStopAsync(string taskName)
    {
        try
        {
            _localCache.Remove(taskName);
            await store.SetStoppedAsync(taskName, CancellationToken.None);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
