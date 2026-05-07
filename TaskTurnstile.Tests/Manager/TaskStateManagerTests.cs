using TaskTurnstile.DependencyInjection;
using TaskTurnstile.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace TaskTurnstile.Tests.Manager;

/// <summary>Unit tests for TaskStateManager using a mock ITaskStateStore.</summary>
public class TaskStateManagerTests
{
    private static (TaskStateManager manager, ITaskStateStore store) BuildWithMockStore(
        TaskTurnstileOptions? options = null)
    {
        var store = Substitute.For<ITaskStateStore>();
        var manager = new TaskStateManager(store, options ?? new TaskTurnstileOptions());
        return (manager, store);
    }

    private static TaskStateManager BuildWithRealStore()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var store = new DistributedCacheTaskStateStore(cache);
        return new TaskStateManager(store, new TaskTurnstileOptions());
    }

    // ── CanStartAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CanStartAsync_ReturnsTrue_WhenNotRunning()
    {
        var (manager, store) = BuildWithMockStore();
        store.IsRunningAsync("job", Arg.Any<CancellationToken>()).Returns(false);

        Assert.True(await manager.CanStartAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanStartAsync_ReturnsFalse_WhenRunningAndNotExpired()
    {
        var (manager, store) = BuildWithMockStore();
        store.IsRunningAsync("job", Arg.Any<CancellationToken>()).Returns(true);
        store.IsExpiredAsync("job", Arg.Any<CancellationToken>()).Returns(false);

        Assert.False(await manager.CanStartAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanStartAsync_ReturnsTrue_WhenRunningButExpired()
    {
        var (manager, store) = BuildWithMockStore();
        store.IsRunningAsync("job", Arg.Any<CancellationToken>()).Returns(true);
        store.IsExpiredAsync("job", Arg.Any<CancellationToken>()).Returns(true);

        Assert.True(await manager.CanStartAsync("job", TestContext.Current.CancellationToken));
    }

    // ── IsRunningAsync reflects store ─────────────────────────────────────────

    [Fact]
    public async Task IsRunningAsync_ReturnsTrue_IgnoresExpiry()
    {
        var (manager, store) = BuildWithMockStore();
        store.IsRunningAsync("job", Arg.Any<CancellationToken>()).Returns(true);

        // IsRunningAsync is a raw query — it does NOT check expiry
        Assert.True(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
        await store.DidNotReceive().IsExpiredAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── StartAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_ReturnsTrue_AndSetsRunning_WhenFree()
    {
        var manager = BuildWithRealStore();

        var result = await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenAlreadyRunning()
    {
        var manager = BuildWithRealStore();
        await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        var result = await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task StartAsync_UsesDefaultMaxRuntime_WhenNotProvided()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var manager = new TaskStateManager(
            new DistributedCacheTaskStateStore(cache),
            new TaskTurnstileOptions { DefaultMaxRuntime = TimeSpan.FromMilliseconds(50) });

        await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));

        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.False(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── TryStopAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TryStopAsync_ReturnsTrue_WhenRunning()
    {
        var manager = BuildWithRealStore();
        await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(await manager.TryStopAsync("job"));
        Assert.False(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryStopAsync_ReturnsTrue_WhenNotRunning_IsIdempotent()
    {
        var manager = BuildWithRealStore();
        Assert.True(await manager.TryStopAsync("job"));
    }

    [Fact]
    public async Task TryStopAsync_ReturnsFalse_WhenStoreFails()
    {
        var (manager, store) = BuildWithMockStore();
        store.SetStoppedAsync("job", Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("store failure"));

        Assert.False(await manager.TryStopAsync("job"));
    }

    // ── TryRunAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TryRunAsync_ReturnsFalse_WhenAlreadyRunning()
    {
        var manager = BuildWithRealStore();
        await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        var workRan = false;
        var result = await manager.TryRunAsync("job", _ => { workRan = true; return Task.CompletedTask; }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(workRan);
    }

    [Fact]
    public async Task TryRunAsync_RunsWorkAndReturnsTrue_WhenFree()
    {
        var manager = BuildWithRealStore();

        var workRan = false;
        var result = await manager.TryRunAsync("job", _ => { workRan = true; return Task.CompletedTask; }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(workRan);
    }

    [Fact]
    public async Task TryRunAsync_StopsTask_EvenWhenWorkThrows()
    {
        var manager = BuildWithRealStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.TryRunAsync("job", _ => throw new InvalidOperationException("boom"), cancellationToken: TestContext.Current.CancellationToken));

        // Task must be stopped after the exception so it can run again
        Assert.False(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryRunAsync_Generic_ReturnsSkipped_WhenAlreadyRunning()
    {
        var manager = BuildWithRealStore();
        await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        var result = await manager.TryRunAsync<int>("job", _ => Task.FromResult(42), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Started);
        Assert.Equal(TryRunResult<int>.Skipped, result);
    }

    [Fact]
    public async Task TryRunAsync_Generic_ReturnsRanWithValue_WhenFree()
    {
        var manager = BuildWithRealStore();

        var result = await manager.TryRunAsync<int>("job", _ => Task.FromResult(42), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(42, result.Value);
    }

    // ── RunAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WaitsUntilFree_ThenRuns()
    {
        var manager = BuildWithRealStore();
        await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        var workRan = false;

        _ = Task.Run(async () =>
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            await manager.TryStopAsync("job");
        }, TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await manager.RunAsync("job", _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: cts.Token);

        Assert.True(workRan);
    }

    [Fact]
    public async Task RunAsync_StopsTask_EvenWhenWorkThrows()
    {
        var manager = BuildWithRealStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.RunAsync("job", _ => throw new InvalidOperationException("boom"), cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── WaitAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WaitAsync_ThrowsTimeoutException_WhenMaxWaitExceeded()
    {
        var manager = BuildWithRealStore();
        await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<TimeoutException>(() =>
            manager.WaitAsync("job", pollIntervalMs: 10, maxWaitMs: 50, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitAsync_ThrowsOperationCanceled_WhenTokenCancelled()
    {
        var manager = BuildWithRealStore();
        await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            manager.WaitAsync("job", pollIntervalMs: 10, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task WaitAsync_ReturnsImmediately_WhenAlreadyFree()
    {
        var manager = BuildWithRealStore();
        // Should not block or throw
        await manager.WaitAsync("job", pollIntervalMs: 10, maxWaitMs: 500, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── Name isolation ────────────────────────────────────────────────────────

    [Fact]
    public async Task DifferentTaskNames_DoNotBlockEachOther()
    {
        var manager = BuildWithRealStore();

        var startedA = await manager.StartAsync("job-a", cancellationToken: TestContext.Current.CancellationToken);
        var startedB = await manager.StartAsync("job-b", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(startedA);
        Assert.True(startedB);
    }

    // ── Concurrency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_ConcurrentCalls_OnlyOneSucceeds()
    {
        var manager = BuildWithRealStore();

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken))
            .ToList();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(19, results.Count(r => !r));
    }

    [Fact]
    public async Task TryRunAsync_ConcurrentCalls_WorkExecutedExactlyOnce()
    {
        var manager = BuildWithRealStore();
        var callCount = 0;

        var tasks = Enumerable.Range(0, 20).Select(_ =>
            manager.TryRunAsync("job", async _ =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(20);
            }, cancellationToken: TestContext.Current.CancellationToken)).ToList();

        await Task.WhenAll(tasks);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task RunAsync_TwoConcurrentCalls_BothEventuallyRun()
    {
        var manager = BuildWithRealStore();
        var callCount = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var t1 = manager.RunAsync("job", async _ =>
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(30);
        }, cancellationToken: cts.Token);

        var t2 = manager.RunAsync("job", async _ =>
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(30);
        }, cancellationToken: cts.Token);

        await Task.WhenAll(t1, t2);

        Assert.Equal(2, callCount);
    }

    // ── TryRunAsync<T> exception handling ────────────────────────────────────

    [Fact]
    public async Task TryRunAsync_Generic_StopsTask_EvenWhenWorkThrows()
    {
        var manager = BuildWithRealStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.TryRunAsync<int>("job", _ => throw new InvalidOperationException("boom"), cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── StartAsync with explicit maxRuntime ───────────────────────────────────

    [Fact]
    public async Task StartAsync_ExplicitMaxRuntime_ExpiresAfterElapsed()
    {
        var manager = BuildWithRealStore();

        await manager.StartAsync("job", maxRuntime: TimeSpan.FromMilliseconds(50), cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));

        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.False(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── WaitAsync acquires the task ───────────────────────────────────────────

    [Fact]
    public async Task WaitAsync_MarksTaskAsRunning_AfterReturning()
    {
        var manager = BuildWithRealStore();

        await manager.WaitAsync("job", pollIntervalMs: 10, maxWaitMs: 500, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(await manager.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── RunAsync cancellation while waiting ───────────────────────────────────

    [Fact]
    public async Task RunAsync_ThrowsOperationCanceled_WhenCancelledWhileWaiting()
    {
        var manager = BuildWithRealStore();
        await manager.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            manager.RunAsync("job", _ => Task.CompletedTask, cancellationToken: cts.Token));
    }

    // ── Multi-manager coordination ────────────────────────────────────────────

    private static (TaskStateManager managerA, TaskStateManager managerB) BuildTwoManagersWithSharedStore()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var store = new DistributedCacheTaskStateStore(cache);
        return (
            new TaskStateManager(store, new TaskTurnstileOptions()),
            new TaskStateManager(store, new TaskTurnstileOptions())
        );
    }

    [Fact]
    public async Task TwoManagers_SharedStore_OnlyOneCanStart()
    {
        var (managerA, managerB) = BuildTwoManagersWithSharedStore();

        var startedA = await managerA.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);
        var startedB = await managerB.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(startedA);
        Assert.False(startedB);
    }

    [Fact]
    public async Task TwoManagers_SharedStore_ManagerBCanStartAfterManagerAStops()
    {
        var (managerA, managerB) = BuildTwoManagersWithSharedStore();

        await managerA.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);
        await managerA.TryStopAsync("job");

        Assert.True(await managerB.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TwoManagers_SharedStore_TryRunAsync_OnlyOneExecutesWork()
    {
        var (managerA, managerB) = BuildTwoManagersWithSharedStore();
        var callCount = 0;

        var t1 = managerA.TryRunAsync("job", async _ => { Interlocked.Increment(ref callCount); await Task.Delay(30); }, cancellationToken: TestContext.Current.CancellationToken);
        var t2 = managerB.TryRunAsync("job", async _ => { Interlocked.Increment(ref callCount); await Task.Delay(30); }, cancellationToken: TestContext.Current.CancellationToken);

        await Task.WhenAll(t1, t2);

        Assert.Equal(1, callCount);
    }
}
