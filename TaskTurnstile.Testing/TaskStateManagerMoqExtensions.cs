using Moq;
using Moq.Language.Flow;

namespace TaskTurnstile.Testing;

/// <summary>
/// Moq setup extensions for <see cref="ITaskStateManager"/> that eliminate boilerplate
/// on the methods that accept a <see cref="Func{T, TResult}"/> delegate.
/// </summary>
/// <remarks>
/// <para>
/// The methods that take a <c>Func&lt;CancellationToken, Task&gt;</c> require verbose
/// <c>Returns&lt;string, Func&lt;...&gt;, TimeSpan?, CancellationToken&gt;</c> calls.
/// These extensions collapse that to a single line:
/// </para>
/// <code>
/// // Before
/// Mocker.GetMock&lt;ITaskStateManager&gt;()
///     .Setup(m => m.TryRunAsync(
///         It.IsAny&lt;string&gt;(),
///         It.IsAny&lt;Func&lt;CancellationToken, Task&gt;&gt;(),
///         It.IsAny&lt;TimeSpan?&gt;(),
///         It.IsAny&lt;CancellationToken&gt;()))
///     .Returns&lt;string, Func&lt;CancellationToken, Task&gt;, TimeSpan?, CancellationToken&gt;(
///         async (_, work, _, ct) => { await work(ct); return true; });
///
/// // After
/// Mocker.GetMock&lt;ITaskStateManager&gt;().SetupTryRunAsync(returns: true);
/// </code>
/// <para>
/// <c>Verify</c> calls are unchanged — they already work naturally.
/// </para>
/// </remarks>
public static class TaskStateManagerMoqExtensions
{
    /// <summary>
    /// Sets up <see cref="ITaskStateManager.TryRunAsync(string, Func{CancellationToken, Task}, TimeSpan?, CancellationToken)"/>.
    /// When <paramref name="returns"/> is <c>true</c>, the <c>work</c> delegate is invoked and the call returns <c>true</c>.
    /// When <paramref name="returns"/> is <c>false</c>, work is skipped and the call returns <c>false</c>.
    /// </summary>
    /// <param name="mock">The <see cref="Mock{T}"/> to configure.</param>
    /// <param name="returns">
    /// Whether the mock should run the work and return <c>true</c>, or skip and return <c>false</c>.
    /// </param>
    /// <param name="taskName">
    /// The task name to match. When <c>null</c> (default), matches any task name.
    /// </param>
    public static IReturnsResult<ITaskStateManager> SetupTryRunAsync(
        this Mock<ITaskStateManager> mock,
        bool returns,
        string? taskName = null)
    {
        var setup = taskName is null
            ? mock.Setup(m => m.TryRunAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            : mock.Setup(m => m.TryRunAsync(
                taskName,
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()));

        if (returns)
        {
            return setup.Returns<string, Func<CancellationToken, Task>, TimeSpan?, CancellationToken>(
                async (_, work, _, ct) =>
                {
                    await work(ct);
                    return true;
                });
        }

        return setup.ReturnsAsync(false);
    }

    /// <summary>
    /// Sets up <see cref="ITaskStateManager.TryRunAsync{T}(string, Func{CancellationToken, Task{T}}, TimeSpan?, CancellationToken)"/>
    /// to run the work delegate and return <see cref="TryRunResult{T}.Ran"/> wrapping <paramref name="value"/>.
    /// </summary>
    /// <param name="mock">The <see cref="Mock{T}"/> to configure.</param>
    /// <param name="value">The value to wrap in <see cref="TryRunResult{T}.Ran"/>.</param>
    /// <param name="taskName">
    /// The task name to match. When <c>null</c> (default), matches any task name.
    /// </param>
    public static IReturnsResult<ITaskStateManager> SetupTryRunAsync<T>(
        this Mock<ITaskStateManager> mock,
        T value,
        string? taskName = null)
    {
        var setup = taskName is null
            ? mock.Setup(m => m.TryRunAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<T>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            : mock.Setup(m => m.TryRunAsync(
                taskName,
                It.IsAny<Func<CancellationToken, Task<T>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()));

        return setup.Returns<string, Func<CancellationToken, Task<T>>, TimeSpan?, CancellationToken>(
            async (_, work, _, ct) =>
            {
                await work(ct);
                return TryRunResult<T>.Ran(value);
            });
    }

    /// <summary>
    /// Sets up <see cref="ITaskStateManager.TryRunAsync{T}(string, Func{CancellationToken, Task{T}}, TimeSpan?, CancellationToken)"/>
    /// to skip work and return <see cref="TryRunResult{T}.Skipped"/>.
    /// </summary>
    /// <param name="mock">The <see cref="Mock{T}"/> to configure.</param>
    /// <param name="taskName">
    /// The task name to match. When <c>null</c> (default), matches any task name.
    /// </param>
    public static IReturnsResult<ITaskStateManager> SetupTryRunAsyncToSkip<T>(
        this Mock<ITaskStateManager> mock,
        string? taskName = null)
    {
        var setup = taskName is null
            ? mock.Setup(m => m.TryRunAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<T>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            : mock.Setup(m => m.TryRunAsync(
                taskName,
                It.IsAny<Func<CancellationToken, Task<T>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()));

        return setup.ReturnsAsync(TryRunResult<T>.Skipped);
    }

    /// <summary>
    /// Sets up <see cref="ITaskStateManager.RunAsync"/> to invoke the work delegate.
    /// </summary>
    /// <param name="mock">The <see cref="Mock{T}"/> to configure.</param>
    /// <param name="taskName">
    /// The task name to match. When <c>null</c> (default), matches any task name.
    /// </param>
    public static IReturnsResult<ITaskStateManager> SetupRunAsync(
        this Mock<ITaskStateManager> mock,
        string? taskName = null)
    {
        var setup = taskName is null
            ? mock.Setup(m => m.RunAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            : mock.Setup(m => m.RunAsync(
                taskName,
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()));

        return setup.Returns<string, Func<CancellationToken, Task>, TimeSpan?, CancellationToken>(
            async (_, work, _, ct) => await work(ct));
    }
}
