using Moq;
using TaskTurnstile.Testing.Moq;

namespace TaskTurnstile.Tests.Testing;

public class TaskStateManagerMoqExtensionsTests
{
    // ── SetupTryRunAsync (bool) ───────────────────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsync_ReturnsTrue_RunsWork()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(returns: true);

        var workRan = false;
        var result = await mock.Object.TryRunAsync("job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_ReturnsFalse_SkipsWork()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(returns: false);

        var workRan = false;
        var result = await mock.Object.TryRunAsync("job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
        Assert.False(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_ReturnsTrue_PassesCancellationTokenToWork()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(returns: true);

        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;

        await mock.Object.TryRunAsync("job",
            ct => { captured = ct; return Task.CompletedTask; },
            cancellationToken: cts.Token);

        Assert.Equal(cts.Token, captured);
    }

    [Fact]
    public async Task SetupTryRunAsync_WithTaskKey_OnlyMatchesSpecifiedKey()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(returns: true, taskKey: "import-job");

        var workRan = false;
        await mock.Object.TryRunAsync("import-job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_WithTaskKey_DoesNotMatchOtherKey()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(returns: true, taskKey: "import-job");

        // No setup for "other-job" — returns default (false)
        var result = await mock.Object.TryRunAsync("other-job",
            _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task SetupTryRunAsync_WithObjectKey_RunsWork()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(returns: true, taskKey: 42);

        var workRan = false;
        var result = await mock.Object.TryRunAsync(42,
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result);
        Assert.True(workRan);
    }

    // ── SetupTryRunAsync<T> ───────────────────────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsync_Generic_RunsWorkAndReturnsRanWithValue()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(value: 42);

        var workRan = false;
        var result = await mock.Object.TryRunAsync<int>("job",
            _ => { workRan = true; return Task.FromResult(0); },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(42, result.Value);
        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_Generic_WithTaskKey_OnlyMatchesSpecifiedKey()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(value: 99, taskKey: "my-job");

        var result = await mock.Object.TryRunAsync<int>("my-job",
            _ => Task.FromResult(0),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public async Task SetupTryRunAsync_Generic_WithObjectKey_RunsWorkAndReturnsRanWithValue()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(value: 99, taskKey: 42);

        var result = await mock.Object.TryRunAsync<int>(42,
            _ => Task.FromResult(0),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Started);
        Assert.Equal(99, result.Value);
    }

    // ── SetupTryRunAsyncToSkip<T> ─────────────────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsyncToSkip_ReturnsSkipped_WorkNotRun()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsyncToSkip<int>();

        var workRan = false;
        var result = await mock.Object.TryRunAsync<int>("job",
            _ => { workRan = true; return Task.FromResult(0); },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TryRunResult<int>.Skipped, result);
        Assert.False(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsyncToSkip_WithTaskKey_OnlyMatchesSpecifiedKey()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsyncToSkip<int>(taskKey: "skip-job");

        var result = await mock.Object.TryRunAsync<int>("skip-job",
            _ => Task.FromResult(0),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TryRunResult<int>.Skipped, result);
    }

    [Fact]
    public async Task SetupTryRunAsyncToSkip_WithObjectKey_ReturnsSkipped()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsyncToSkip<int>(taskKey: 42);

        var workRan = false;
        var result = await mock.Object.TryRunAsync<int>(42,
            _ => { workRan = true; return Task.FromResult(0); },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TryRunResult<int>.Skipped, result);
        Assert.False(workRan);
    }

    // ── SetupRunAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetupRunAsync_RunsWork()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupRunAsync();

        var workRan = false;
        await mock.Object.RunAsync("job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupRunAsync_PassesCancellationTokenToWork()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupRunAsync();

        using var cts = new CancellationTokenSource();
        CancellationToken captured = default;

        await mock.Object.RunAsync("job",
            ct => { captured = ct; return Task.CompletedTask; },
            cancellationToken: cts.Token);

        Assert.Equal(cts.Token, captured);
    }

    [Fact]
    public async Task SetupRunAsync_WithTaskKey_OnlyMatchesSpecifiedKey()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupRunAsync(taskKey: "export-job");

        var workRan = false;
        await mock.Object.RunAsync("export-job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupRunAsync_WithObjectKey_RunsWork()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupRunAsync(taskKey: 42);

        var workRan = false;
        await mock.Object.RunAsync(42,
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    // ── Verify still works after setup ───────────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsync_VerifyByTaskKey_Works()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(returns: true);

        await mock.Object.TryRunAsync("import-job",
            _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);

        mock.Verify(m => m.TryRunAsync(
            "import-job",
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
