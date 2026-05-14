using Moq;
using TaskTurnstile.Testing;

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
    public async Task SetupTryRunAsync_WithTaskName_OnlyMatchesSpecifiedName()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(returns: true, taskName: "import-job");

        var workRan = false;
        await mock.Object.TryRunAsync("import-job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    [Fact]
    public async Task SetupTryRunAsync_WithTaskName_DoesNotMatchOtherName()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(returns: true, taskName: "import-job");

        // No setup for "other-job" — returns default (false)
        var result = await mock.Object.TryRunAsync("other-job",
            _ => Task.CompletedTask,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result);
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
    public async Task SetupTryRunAsync_Generic_WithTaskName_OnlyMatchesSpecifiedName()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsync(value: 99, taskName: "my-job");

        var result = await mock.Object.TryRunAsync<int>("my-job",
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
    public async Task SetupTryRunAsyncToSkip_WithTaskName_OnlyMatchesSpecifiedName()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupTryRunAsyncToSkip<int>(taskName: "skip-job");

        var result = await mock.Object.TryRunAsync<int>("skip-job",
            _ => Task.FromResult(0),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TryRunResult<int>.Skipped, result);
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
    public async Task SetupRunAsync_WithTaskName_OnlyMatchesSpecifiedName()
    {
        var mock = new Mock<ITaskStateManager>();
        mock.SetupRunAsync(taskName: "export-job");

        var workRan = false;
        await mock.Object.RunAsync("export-job",
            _ => { workRan = true; return Task.CompletedTask; },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(workRan);
    }

    // ── Verify still works after setup ───────────────────────────────────────

    [Fact]
    public async Task SetupTryRunAsync_VerifyByTaskName_Works()
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
