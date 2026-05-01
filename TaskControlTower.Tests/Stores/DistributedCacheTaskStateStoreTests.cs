using TaskControlTower.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TaskControlTower.Tests.Stores;

public class DistributedCacheTaskStateStoreTests
{
    private static DistributedCacheTaskStateStore BuildStore()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
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
        var store = BuildStore();

        await store.SetRunningAsync("job", maxRuntime: TimeSpan.FromMilliseconds(50));
        Assert.True(await store.IsRunningAsync("job"));

        await Task.Delay(150);
        Assert.False(await store.IsRunningAsync("job"));
    }

    [Fact]
    public async Task SetRunningAsync_WithoutMaxRuntime_EntryDoesNotExpire()
    {
        var store = BuildStore();

        await store.SetRunningAsync("job", maxRuntime: null);
        await Task.Delay(100);
        Assert.True(await store.IsRunningAsync("job"));
    }

    // ── SetStoppedAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetStoppedAsync_IsIdempotent_WhenCalledWhenNotRunning()
    {
        var store = BuildStore();
        await store.SetStoppedAsync("job"); // should not throw
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

    // ── KeyPrefix isolation ───────────────────────────────────────────────────

    [Fact]
    public async Task KeyPrefix_DifferentPrefixes_DoNotCollide()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var storeA = new DistributedCacheTaskStateStore(cache, "a:");
        var storeB = new DistributedCacheTaskStateStore(cache, "b:");

        await storeA.SetRunningAsync("job");

        Assert.True(await storeA.IsRunningAsync("job"));
        Assert.False(await storeB.IsRunningAsync("job"));
    }

    [Fact]
    public async Task KeyPrefix_SamePrefix_CoordinatesAcrossInstances()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var storeA = new DistributedCacheTaskStateStore(cache, "shared:");
        var storeB = new DistributedCacheTaskStateStore(cache, "shared:");

        await storeA.SetRunningAsync("job");

        Assert.True(await storeB.IsRunningAsync("job"));
    }
}
