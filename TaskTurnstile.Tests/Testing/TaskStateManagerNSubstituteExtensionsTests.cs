using NSubstitute;
using TaskTurnstile.Testing.NSubstitute;

namespace TaskTurnstile.Tests.Testing;

public class TaskStateManagerNSubstituteExtensionsTests
{
    // ── SetupTryRunAsync (bool) ───────────────────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsync_ReturnsTrue_RunsWork()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(returns: true);

        var workRan = false;
        var result = await sub.TryRunAsync("job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_ReturnsFalse_SkipsWork()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(returns: false);

        var workRan = false;
        var result = await sub.TryRunAsync("job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_ReturnsTrue_PassesCancellationTokenToWork()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(returns: true);

        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;

        await sub.TryRunAsync("job",
            ct => { captured = ct; return Task.CompletedTask; },
            cancellationToken: cts.Token);

        Assert.Equal(cts.Token, captured);
    }

    [Fact]
    public async Task SetupTryRunAsync_WithTaskKey_OnlyMatchesSpecifiedKey()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(returns: true, taskKey: "import-job");

        var workRan = false;
        await sub.TryRunAsync("import-job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_WithTaskKey_DoesNotMatchOtherKey()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(returns: true, taskKey: "import-job");

        // No setup for "other-job" — returns default (false)
        var result = await sub.TryRunAsync("other-job",
            _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task SetupTryRunAsync_WithObjectKey_RunsWork()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(returns: true, taskKey: 42);

        var workRan = false;
        var result = await sub.TryRunAsync(42,
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(workRan);
    }

    // ── SetupTryRunAsync<T> ───────────────────────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsync_Generic_RunsWorkAndReturnsRanWithValue()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(value: 42);

        var workRan = false;
        var result = await sub.TryRunAsync<int>("job",
            _ => { workRan = true; return Task.FromResult(0); },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(42, result.Value);
        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_Generic_WithTaskKey_OnlyMatchesSpecifiedKey()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(value: 99, taskKey: "my-job");

        var result = await sub.TryRunAsync<int>("my-job",
            _ => Task.FromResult(0),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public async Task SetupTryRunAsync_Generic_WithObjectKey_RunsWorkAndReturnsRanWithValue()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(value: 99, taskKey: 42);

        var result = await sub.TryRunAsync<int>(42,
            _ => Task.FromResult(0),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(99, result.Value);
    }

    // ── SetupTryRunAsyncToSkip<T> ─────────────────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsyncToSkip_ReturnsSkipped_WorkNotRun()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsyncToSkip<int>();

        var workRan = false;
        var result = await sub.TryRunAsync<int>("job",
            _ => { workRan = true; return Task.FromResult(0); },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TryRunResult<int>.Skipped, result);
        Assert.False(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsyncToSkip_WithTaskKey_OnlyMatchesSpecifiedKey()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsyncToSkip<int>(taskKey: "skip-job");

        var result = await sub.TryRunAsync<int>("skip-job",
            _ => Task.FromResult(0),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TryRunResult<int>.Skipped, result);
    }

    [Fact]
    public async Task SetupTryRunAsyncToSkip_WithObjectKey_ReturnsSkipped()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsyncToSkip<int>(taskKey: 42);

        var workRan = false;
        var result = await sub.TryRunAsync<int>(42,
            _ => { workRan = true; return Task.FromResult(0); },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TryRunResult<int>.Skipped, result);
        Assert.False(workRan);
    }

    // ── SetupRunAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetupRunAsync_RunsWork()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupRunAsync();

        var workRan = false;
        await sub.RunAsync("job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupRunAsync_PassesCancellationTokenToWork()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupRunAsync();

        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;

        await sub.RunAsync("job",
            ct => { captured = ct; return Task.CompletedTask; },
            cancellationToken: cts.Token);

        Assert.Equal(cts.Token, captured);
    }

    [Fact]
    public async Task SetupRunAsync_WithTaskKey_OnlyMatchesSpecifiedKey()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupRunAsync(taskKey: "export-job");

        var workRan = false;
        await sub.RunAsync("export-job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupRunAsync_WithObjectKey_RunsWork()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupRunAsync(taskKey: 42);

        var workRan = false;
        await sub.RunAsync(42,
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    // ── Received() still works after setup ───────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsync_ReceivedByTaskKey_Works()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(returns: true);

        await sub.TryRunAsync("import-job",
            _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);

        await sub.Received(1).TryRunAsync(
            "import-job",
            Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());
    }
}
