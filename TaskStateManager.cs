using System.Diagnostics;
using AsyncKeyedLock;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TaskTurnstile;

internal sealed class TaskStateManager(ITaskStateStore store, TaskTurnstileOptions options) : ITaskStateManager
{
    private readonly AsyncKeyedLocker<string> _locker = new();
    private readonly IMemoryCache _localCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
    private const int DefaultPollIntervalMs = 250;

    public Task<bool> IsRunningAsync(object taskKey, CancellationToken cancellationToken = default) =>
        store.IsRunningAsync(TaskKeyConverter.ToKey(taskKey), cancellationToken);

    public Task<bool> CanStartAsync(object taskKey, CancellationToken cancellationToken = default) =>
        CanStartCoreAsync(TaskKeyConverter.ToKey(taskKey), cancellationToken);

    private async Task<bool> CanStartCoreAsync(string key, CancellationToken cancellationToken)
    {
        if (!await store.IsRunningAsync(key, cancellationToken))
            return true;

        return await store.IsExpiredAsync(key, cancellationToken);
    }

    public async Task RunAsync(object taskKey, Func<CancellationToken, Task> work, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        var key = TaskKeyConverter.ToKey(taskKey);
        await WaitAndStartAsync(key, maxRuntime ?? options.DefaultMaxRuntime, DefaultPollIntervalMs, null, cancellationToken);
        try
        {
            await work(cancellationToken);
        }
        finally
        {
            await StopAsync(key);
        }
    }

    public async Task<bool> TryRunAsync(object taskKey, Func<CancellationToken, Task> work, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        var key = TaskKeyConverter.ToKey(taskKey);
        if (!await StartCoreAsync(key, maxRuntime, cancellationToken))
            return false;

        try
        {
            await work(cancellationToken);
            return true;
        }
        finally
        {
            await StopAsync(key);
        }
    }

    public async Task<TryRunResult<T>> TryRunAsync<T>(object taskKey, Func<CancellationToken, Task<T>> work, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        var key = TaskKeyConverter.ToKey(taskKey);
        if (!await StartCoreAsync(key, maxRuntime, cancellationToken))
            return TryRunResult<T>.Skipped;

        try
        {
            var value = await work(cancellationToken);
            return TryRunResult<T>.Ran(value);
        }
        finally
        {
            await StopAsync(key);
        }
    }

    public Task WaitAsync(object taskKey, CancellationToken cancellationToken = default) =>
        WaitAndStartAsync(TaskKeyConverter.ToKey(taskKey), options.DefaultMaxRuntime, DefaultPollIntervalMs, null, cancellationToken);

    public Task WaitAsync(object taskKey, int pollIntervalMs, int? maxWaitMs = null, CancellationToken cancellationToken = default) =>
        WaitAndStartAsync(TaskKeyConverter.ToKey(taskKey), options.DefaultMaxRuntime, pollIntervalMs, maxWaitMs, cancellationToken);

    private async Task WaitAndStartAsync(string key, TimeSpan? maxRuntime, int pollIntervalMs, int? maxWaitMs, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Fast path: if this instance started the task and it hasn't expired locally,
            // skip the backing store query entirely.
            if (_localCache.TryGetValue(key, out _))
            {
                if (maxWaitMs.HasValue && stopwatch.ElapsedMilliseconds >= maxWaitMs.Value)
                    throw new TimeoutException($"Task '{key}' did not become available within {maxWaitMs.Value}ms.");

                await Task.Delay(pollIntervalMs, cancellationToken);
                continue;
            }

            if (await StartCoreAsync(key, maxRuntime, cancellationToken))
                return;

            if (maxWaitMs.HasValue && stopwatch.ElapsedMilliseconds >= maxWaitMs.Value)
                throw new TimeoutException($"Task '{key}' did not become available within {maxWaitMs.Value}ms.");

            await Task.Delay(pollIntervalMs, cancellationToken);
        }
    }

    public async Task<bool> StartAsync(object taskKey, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default) =>
        await StartCoreAsync(TaskKeyConverter.ToKey(taskKey), maxRuntime, cancellationToken);

    private async Task<bool> StartCoreAsync(string key, TimeSpan? maxRuntime, CancellationToken cancellationToken)
    {
        using (await _locker.LockAsync(key, cancellationToken))
        {
            if (!await CanStartCoreAsync(key, cancellationToken))
                return false;

            await store.SetRunningAsync(key, maxRuntime ?? options.DefaultMaxRuntime, cancellationToken);

            var effectiveRuntime = maxRuntime ?? options.DefaultMaxRuntime;
            if (effectiveRuntime.HasValue)
                _localCache.Set(key, true, absoluteExpirationRelativeToNow: effectiveRuntime.Value);
            else
                _localCache.Set(key, true);

            return true;
        }
    }

    public async Task<bool> TryStopAsync(object taskKey)
    {
        var key = TaskKeyConverter.ToKey(taskKey);
        try
        {
            _localCache.Remove(key);
            await store.SetStoppedAsync(key, CancellationToken.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Task StopAsync(string key)
    {
        _localCache.Remove(key);
        return store.SetStoppedAsync(key, CancellationToken.None);
    }
}
