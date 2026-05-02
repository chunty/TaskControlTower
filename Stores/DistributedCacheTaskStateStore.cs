using Microsoft.Extensions.Caching.Distributed;

namespace TaskTurnstile.Stores;

internal sealed class DistributedCacheTaskStateStore(IDistributedCache cache, string keyPrefix = "cm:") : ITaskStateStore
{
    private static readonly byte[] Sentinel = [1];

    // Prefix all keys to avoid collisions when sharing the app's IDistributedCache.
    private string Key(string taskName) => $"{keyPrefix}{taskName}";

    public async Task<bool> IsRunningAsync(string taskName, CancellationToken cancellationToken = default) =>
        await cache.GetAsync(Key(taskName), cancellationToken) != null;

    // IDistributedCache auto-evicts expired entries — they simply disappear.
    // An expired entry will already return null from GetAsync, so CanStartAsync
    // naturally returns true without needing a separate expiry check.
    public Task<bool> IsExpiredAsync(string taskName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task SetRunningAsync(string taskName, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions();
        if (maxRuntime.HasValue)
            options.AbsoluteExpirationRelativeToNow = maxRuntime;

        return cache.SetAsync(Key(taskName), Sentinel, options, cancellationToken);
    }

    public Task SetStoppedAsync(string taskName, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(Key(taskName), cancellationToken);

    // IDistributedCache handles expiry automatically — nothing to clean up.
    public Task CleanupAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
