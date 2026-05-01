using ConcurrencyManager.Stores;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.TimeProvider.Testing;

namespace ConcurrencyManager.Tests.Stores;

public class DistributedCacheTaskStateStoreTests
{
    private static DistributedCacheTaskStateStore BuildStore(FakeTimeProvider? timeProvider = null)
    {
        var cacheOptions = new MemoryDistributedCacheOptions();
        if (timeProvider is not null)
            cacheOptions.TimeProvider = timeProvider;

        var cache = new MemoryDistributedCache(Options.Create(cacheOptions));
        return new DistributedCacheTaskStateStore(cache);
    }

    // ── IsRunningAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task IsRunningAsync_ReturnsFalse_WhenNotStarted()
    {
        var store = BuildStore();
        Assert.False(await store.IsRunningAsync("job"));
    }

    [Fact]
    public async Task IsRunningAsync_ReturnsTrue_AfterSetRunning()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job");
        Assert.True(await store.IsRunningAsync("job"));
    }

    [Fact]
    public async Task IsRunningAsync_ReturnsFalse_AfterSetStopped()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job");
        await store.SetStoppedAsync("job");
        Assert.False(await store.IsRunningAsync("job"));
    }

    [Fact]
    public async Task IsRunningAsync_IsCaseSensitive()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job");
        Assert.False(await store.IsRunningAsync("Job"));
        Assert.False(await store.IsRunningAsync("JOB"));
    }

    [Fact]
    public async Task IsRunningAsync_IsolatedByName()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job-a");
        Assert.True(await store.IsRunningAsync("job-a"));
        Assert.False(await store.IsRunningAsync("job-b"));
    }

    // ── IsExpiredAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task IsExpiredAsync_AlwaysReturnsFalse_BecauseCacheAutoEvicts()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job", maxRuntime: TimeSpan.FromMilliseconds(1));
        // Even a very short maxRuntime — IsExpiredAsync is always false;
        // the cache simply makes the entry disappear instead.
        Assert.False(await store.IsExpiredAsync("job"));
    }

    // ── SetRunningAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetRunningAsync_Upserts_CallingTwiceUpdatesEntry()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job");
        await store.SetRunningAsync("job"); // should not throw
        Assert.True(await store.IsRunningAsync("job"));
    }

    [Fact]
    public async Task SetRunningAsync_WithMaxRuntime_EntryExpiresAfterElapsed()
    {
        var time = new FakeTimeProvider();
        var store = BuildStore(time);

        await store.SetRunningAsync("job", maxRuntime: TimeSpan.FromMinutes(1));
        Assert.True(await store.IsRunningAsync("job"));

        time.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));
        Assert.False(await store.IsRunningAsync("job"));
    }

    [Fact]
    public async Task SetRunningAsync_WithoutMaxRuntime_EntryDoesNotExpire()
    {
        var time = new FakeTimeProvider();
        var store = BuildStore(time);

        await store.SetRunningAsync("job", maxRuntime: null);
        time.Advance(TimeSpan.FromDays(365));
        Assert.True(await store.IsRunningAsync("job"));
    }

    // ── SetStoppedAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetStoppedAsync_IsIdempotent_WhenCalledWhenNotRunning()
    {
        var store = BuildStore();
        // Should not throw
        await store.SetStoppedAsync("job");
        Assert.False(await store.IsRunningAsync("job"));
    }

    // ── CleanupAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CleanupAsync_IsNoOp()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job");
        await store.CleanupAsync(); // should not throw or remove non-expired entries
        Assert.True(await store.IsRunningAsync("job"));
    }
}
