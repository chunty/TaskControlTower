using NSubstitute;
using TaskTurnstile.Testing;

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
    public async Task SetupTryRunAsync_WithTaskName_OnlyMatchesSpecifiedName()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(returns: true, taskName: "import-job");

        var workRan = false;
        await sub.TryRunAsync("import-job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_WithTaskName_DoesNotMatchOtherName()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(returns: true, taskName: "import-job");

        // No setup for "other-job" — returns default (false)
        var result = await sub.TryRunAsync("other-job",
            _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
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
    public async Task SetupTryRunAsync_Generic_WithTaskName_OnlyMatchesSpecifiedName()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsync(value: 99, taskName: "my-job");

        var result = await sub.TryRunAsync<int>("my-job",
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
    public async Task SetupTryRunAsyncToSkip_WithTaskName_OnlyMatchesSpecifiedName()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupTryRunAsyncToSkip<int>(taskName: "skip-job");

        var result = await sub.TryRunAsync<int>("skip-job",
            _ => Task.FromResult(0),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TryRunResult<int>.Skipped, result);
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
    public async Task SetupRunAsync_WithTaskName_OnlyMatchesSpecifiedName()
    {
        var sub = Substitute.For<ITaskStateManager>();
        sub.SetupRunAsync(taskName: "export-job");

        var workRan = false;
        await sub.RunAsync("export-job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    // ── Received() still works after setup ───────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsync_ReceivedByTaskName_Works()
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
