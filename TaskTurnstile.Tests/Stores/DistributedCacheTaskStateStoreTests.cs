using TaskTurnstile.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace TaskTurnstile.Tests.Stores;

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
        Assert.False(await store.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRunningAsync_ReturnsTrue_AfterSetRunning()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(await store.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRunningAsync_ReturnsFalse_AfterSetStopped()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job", cancellationToken: TestContext.Current.CancellationToken);
        await store.SetStoppedAsync("job", TestContext.Current.CancellationToken);
        Assert.False(await store.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRunningAsync_IsCaseSensitive()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job", cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(await store.IsRunningAsync("Job", TestContext.Current.CancellationToken));
        Assert.False(await store.IsRunningAsync("JOB", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRunningAsync_IsolatedByName()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job-a", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(await store.IsRunningAsync("job-a", TestContext.Current.CancellationToken));
        Assert.False(await store.IsRunningAsync("job-b", TestContext.Current.CancellationToken));
    }

    // ── IsExpiredAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task IsExpiredAsync_AlwaysReturnsFalse_BecauseCacheAutoEvicts()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job", maxRuntime: TimeSpan.FromMilliseconds(1), cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(await store.IsExpiredAsync("job", TestContext.Current.CancellationToken));
    }

    // ── SetRunningAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetRunningAsync_Upserts_CallingTwiceUpdatesEntry()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job", cancellationToken: TestContext.Current.CancellationToken);
        await store.SetRunningAsync("job", cancellationToken: TestContext.Current.CancellationToken); // should not throw
        Assert.True(await store.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetRunningAsync_WithMaxRuntime_EntryExpiresAfterElapsed()
    {
        var store = BuildStore();

        await store.SetRunningAsync("job", maxRuntime: TimeSpan.FromMilliseconds(50), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(await store.IsRunningAsync("job", TestContext.Current.CancellationToken));

        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.False(await store.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetRunningAsync_WithoutMaxRuntime_EntryDoesNotExpire()
    {
        var store = BuildStore();

        await store.SetRunningAsync("job", maxRuntime: null, cancellationToken: TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.True(await store.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── SetStoppedAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task SetStoppedAsync_IsIdempotent_WhenCalledWhenNotRunning()
    {
        var store = BuildStore();
        await store.SetStoppedAsync("job", TestContext.Current.CancellationToken); // should not throw
        Assert.False(await store.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── CleanupAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CleanupAsync_IsNoOp()
    {
        var store = BuildStore();
        await store.SetRunningAsync("job", cancellationToken: TestContext.Current.CancellationToken);
        await store.CleanupAsync(TestContext.Current.CancellationToken); // should not throw or remove non-expired entries
        Assert.True(await store.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── KeyPrefix isolation ───────────────────────────────────────────────────

    [Fact]
    public async Task KeyPrefix_DifferentPrefixes_DoNotCollide()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var storeA = new DistributedCacheTaskStateStore(cache, "a:");
        var storeB = new DistributedCacheTaskStateStore(cache, "b:");

        await storeA.SetRunningAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(await storeA.IsRunningAsync("job", TestContext.Current.CancellationToken));
        Assert.False(await storeB.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task KeyPrefix_SamePrefix_CoordinatesAcrossInstances()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var storeA = new DistributedCacheTaskStateStore(cache, "shared:");
        var storeB = new DistributedCacheTaskStateStore(cache, "shared:");

        await storeA.SetRunningAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(await storeB.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }
}
