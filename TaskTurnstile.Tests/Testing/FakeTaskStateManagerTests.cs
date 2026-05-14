using TaskTurnstile.Testing;

namespace TaskTurnstile.Tests.Testing;

public class FakeTaskStateManagerTests
{
    // ── IsRunningAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task IsRunningAsync_ReturnsFalse_Initially()
    {
        var fake = new FakeTaskStateManager();
        Assert.False(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsRunningAsync_ReturnsTrue_AfterMarkRunning()
    {
        var fake = new FakeTaskStateManager();
        fake.MarkRunning("job");
        Assert.True(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── CanStartAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CanStartAsync_ReturnsTrue_WhenNotRunning()
    {
        var fake = new FakeTaskStateManager();
        Assert.True(await fake.CanStartAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanStartAsync_ReturnsFalse_WhenRunning()
    {
        var fake = new FakeTaskStateManager();
        fake.MarkRunning("job");
        Assert.False(await fake.CanStartAsync("job", TestContext.Current.CancellationToken));
    }

    // ── TryRunAsync (bool) ────────────────────────────────────────────────────

    [Fact]
    public async Task TryRunAsync_RunsWorkAndReturnsTrue_WhenFree()
    {
        var fake = new FakeTaskStateManager();
        var workRan = false;

        var result = await fake.TryRunAsync("job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(workRan);
    }

    [Fact]
    public async Task TryRunAsync_ReturnsFalse_WhenAlreadyRunning()
    {
        var fake = new FakeTaskStateManager();
        fake.MarkRunning("job");
        var workRan = false;

        var result = await fake.TryRunAsync("job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(workRan);
    }

    [Fact]
    public async Task TryRunAsync_PassesCancellationToken_ToWork()
    {
        var fake = new FakeTaskStateManager();
        using var cts = new CancellationTokenSource();
        CancellationToken capturedToken = default;

        await fake.TryRunAsync("job",
            ct => { capturedToken = ct; return Task.CompletedTask; },
            cancellationToken: cts.Token);

        Assert.Equal(cts.Token, capturedToken);
    }

    [Fact]
    public async Task TryRunAsync_ClearsRunningState_AfterCompletion()
    {
        var fake = new FakeTaskStateManager();
        await fake.TryRunAsync("job", _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryRunAsync_ClearsRunningState_EvenWhenWorkThrows()
    {
        var fake = new FakeTaskStateManager();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.TryRunAsync("job", _ => throw new InvalidOperationException("boom"),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryRunAsync_AllowsSecondRun_AfterFirstCompletes()
    {
        var fake = new FakeTaskStateManager();

        var first = await fake.TryRunAsync("job", _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);
        var second = await fake.TryRunAsync("job", _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(first);
        Assert.True(second);
    }

    // ── TryRunAsync<T> ────────────────────────────────────────────────────────

    [Fact]
    public async Task TryRunAsync_Generic_ReturnsRan_WithValue_WhenFree()
    {
        var fake = new FakeTaskStateManager();

        var result = await fake.TryRunAsync<int>("job", _ => Task.FromResult(42),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TryRunAsync_Generic_ReturnsSkipped_WhenAlreadyRunning()
    {
        var fake = new FakeTaskStateManager();
        fake.MarkRunning("job");

        var result = await fake.TryRunAsync<int>("job", _ => Task.FromResult(42),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TryRunResult<int>.Skipped, result);
    }

    [Fact]
    public async Task TryRunAsync_Generic_ClearsRunningState_EvenWhenWorkThrows()
    {
        var fake = new FakeTaskStateManager();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.TryRunAsync<int>("job", _ => throw new InvalidOperationException("boom"),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    // ── RunAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_RunsWork_WhenFree()
    {
        var fake = new FakeTaskStateManager();
        var workRan = false;

        await fake.RunAsync("job", _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task RunAsync_ClearsRunningState_AfterCompletion()
    {
        var fake = new FakeTaskStateManager();
        await fake.RunAsync("job", _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_ClearsRunningState_EvenWhenWorkThrows()
    {
        var fake = new FakeTaskStateManager();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fake.RunAsync("job", _ => throw new InvalidOperationException("boom"),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.False(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_WaitsUntilFree_ThenRuns()
    {
        var fake = new FakeTaskStateManager();
        fake.MarkRunning("job");

        var workRan = false;

        _ = Task.Run(async () =>
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
            await fake.TryStopAsync("job");
        }, TestContext.Current.CancellationToken);

        await fake.RunAsync("job", _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task RunAsync_ThrowsTimeoutException_WhenBlockedBeyondWaitTimeout()
    {
        var fake = new FakeTaskStateManager { WaitTimeout = TimeSpan.FromMilliseconds(50) };
        fake.MarkRunning("job");

        await Assert.ThrowsAsync<TimeoutException>(() =>
            fake.RunAsync("job", _ => Task.CompletedTask,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_ThrowsOperationCanceled_WhenTokenCancelledWhileWaiting()
    {
        var fake = new FakeTaskStateManager { WaitTimeout = TimeSpan.FromSeconds(30) };
        fake.MarkRunning("job");

        using var cts = new CancellationTokenSource(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fake.RunAsync("job", _ => Task.CompletedTask, cancellationToken: cts.Token));
    }

    // ── StartAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_ReturnsTrue_WhenFree()
    {
        var fake = new FakeTaskStateManager();
        Assert.True(await fake.StartAsync("job",
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_ReturnsFalse_WhenAlreadyRunning()
    {
        var fake = new FakeTaskStateManager();
        await fake.StartAsync("job", cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(await fake.StartAsync("job",
            cancellationToken: TestContext.Current.CancellationToken));
    }

    // ── TryStopAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TryStopAsync_ClearsRunning_ReturnsTrue()
    {
        var fake = new FakeTaskStateManager();
        fake.MarkRunning("job");

        var result = await fake.TryStopAsync("job");

        Assert.True(result);
        Assert.False(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryStopAsync_IsIdempotent_WhenNotRunning()
    {
        var fake = new FakeTaskStateManager();
        Assert.True(await fake.TryStopAsync("job"));
    }

    // ── WaitAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WaitAsync_MarksRunning_WhenFree()
    {
        var fake = new FakeTaskStateManager();
        await fake.WaitAsync("job", TestContext.Current.CancellationToken);
        Assert.True(await fake.IsRunningAsync("job", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitAsync_WithMaxWaitMs_ThrowsTimeoutException_WhenBlocked()
    {
        var fake = new FakeTaskStateManager();
        fake.MarkRunning("job");

        await Assert.ThrowsAsync<TimeoutException>(() =>
            fake.WaitAsync("job", pollIntervalMs: 10, maxWaitMs: 50,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WaitAsync_ThrowsTimeoutException_WhenBlockedBeyondWaitTimeout()
    {
        var fake = new FakeTaskStateManager { WaitTimeout = TimeSpan.FromMilliseconds(50) };
        fake.MarkRunning("job");

        await Assert.ThrowsAsync<TimeoutException>(() =>
            fake.WaitAsync("job", TestContext.Current.CancellationToken));
    }

    // ── MarkRunning / name isolation ──────────────────────────────────────────

    [Fact]
    public async Task MarkRunning_IsolatedByName()
    {
        var fake = new FakeTaskStateManager();
        fake.MarkRunning("job-a");

        Assert.True(await fake.IsRunningAsync("job-a", TestContext.Current.CancellationToken));
        Assert.False(await fake.IsRunningAsync("job-b", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DifferentTaskNames_DoNotBlockEachOther()
    {
        var fake = new FakeTaskStateManager();
        fake.MarkRunning("job-a");

        var ranB = false;
        await fake.TryRunAsync("job-b", _ => { ranB = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(ranB);
    }

    // ── Concurrency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task TryRunAsync_ConcurrentCalls_WorkExecutedExactlyOnce()
    {
        var fake = new FakeTaskStateManager();
        var callCount = 0;

        var tasks = Enumerable.Range(0, 20).Select(i =>
            fake.TryRunAsync("job", async ct =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(20, ct);
            }, cancellationToken: TestContext.Current.CancellationToken)).ToList();

        await Task.WhenAll(tasks);

        Assert.Equal(1, callCount);
    }
}
