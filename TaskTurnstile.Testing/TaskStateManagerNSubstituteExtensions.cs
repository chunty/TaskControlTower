using NSubstitute;
using NSubstitute.Core;

namespace TaskTurnstile.Testing;

/// <summary>
/// NSubstitute setup extensions for <see cref="ITaskStateManager"/> that eliminate boilerplate
/// on the methods that accept a <see cref="Func{T, TResult}"/> delegate.
/// </summary>
/// <remarks>
/// <code>
/// // Before
/// manager.TryRunAsync(
///         Arg.Any&lt;string&gt;(),
///         Arg.Any&lt;Func&lt;CancellationToken, Task&gt;&gt;(),
///         Arg.Any&lt;TimeSpan?&gt;(),
///         Arg.Any&lt;CancellationToken&gt;())
///     .Returns(async ci =>
///     {
///         await ci.Arg&lt;Func&lt;CancellationToken, Task&gt;&gt;()(CancellationToken.None);
///         return true;
///     });
///
/// // After
/// manager.SetupTryRunAsync(returns: true);
/// </code>
/// </remarks>
public static class TaskStateManagerNSubstituteExtensions
{
    /// <summary>
    /// Sets up <see cref="ITaskStateManager.TryRunAsync(string, Func{CancellationToken, Task}, TimeSpan?, CancellationToken)"/>.
    /// When <paramref name="returns"/> is <c>true</c>, the <c>work</c> delegate is invoked and the call returns <c>true</c>.
    /// When <paramref name="returns"/> is <c>false</c>, work is skipped and the call returns <c>false</c>.
    /// </summary>
    /// <param name="manager">The NSubstitute substitute to configure.</param>
    /// <param name="returns">
    /// Whether the substitute should run the work and return <c>true</c>, or skip and return <c>false</c>.
    /// </param>
    /// <param name="taskName">
    /// The task name to match. When <c>null</c> (default), matches any task name.
    /// </param>
    public static ConfiguredCall SetupTryRunAsync(
        this ITaskStateManager manager,
        bool returns,
        string? taskName = null)
    {
        var configuredCall = taskName is null
            ? manager.TryRunAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            : manager.TryRunAsync(
                taskName,
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>());

        if (returns)
        {
            return configuredCall.Returns(async (CallInfo ci) =>
            {
                await ci.Arg<Func<CancellationToken, Task>>()(ci.Arg<CancellationToken>());
                return true;
            });
        }

        return configuredCall.Returns(false);
    }

    /// <summary>
    /// Sets up <see cref="ITaskStateManager.TryRunAsync{T}(string, Func{CancellationToken, Task{T}}, TimeSpan?, CancellationToken)"/>
    /// to run the work delegate and return <see cref="TryRunResult{T}.Ran"/> wrapping <paramref name="value"/>.
    /// </summary>
    /// <param name="manager">The NSubstitute substitute to configure.</param>
    /// <param name="value">The value to wrap in <see cref="TryRunResult{T}.Ran"/>.</param>
    /// <param name="taskName">
    /// The task name to match. When <c>null</c> (default), matches any task name.
    /// </param>
    public static ConfiguredCall SetupTryRunAsync<T>(
        this ITaskStateManager manager,
        T value,
        string? taskName = null)
    {
        var configuredCall = taskName is null
            ? manager.TryRunAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task<T>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            : manager.TryRunAsync(
                taskName,
                Arg.Any<Func<CancellationToken, Task<T>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>());

        return configuredCall.Returns(async (CallInfo ci) =>
        {
            await ci.Arg<Func<CancellationToken, Task<T>>>()(ci.Arg<CancellationToken>());
            return TryRunResult<T>.Ran(value);
        });
    }

    /// <summary>
    /// Sets up <see cref="ITaskStateManager.TryRunAsync{T}(string, Func{CancellationToken, Task{T}}, TimeSpan?, CancellationToken)"/>
    /// to skip work and return <see cref="TryRunResult{T}.Skipped"/>.
    /// </summary>
    /// <param name="manager">The NSubstitute substitute to configure.</param>
    /// <param name="taskName">
    /// The task name to match. When <c>null</c> (default), matches any task name.
    /// </param>
    public static ConfiguredCall SetupTryRunAsyncToSkip<T>(
        this ITaskStateManager manager,
        string? taskName = null)
    {
        var configuredCall = taskName is null
            ? manager.TryRunAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task<T>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            : manager.TryRunAsync(
                taskName,
                Arg.Any<Func<CancellationToken, Task<T>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>());

        return configuredCall.Returns(TryRunResult<T>.Skipped);
    }

    /// <summary>
    /// Sets up <see cref="ITaskStateManager.RunAsync"/> to invoke the work delegate.
    /// </summary>
    /// <param name="manager">The NSubstitute substitute to configure.</param>
    /// <param name="taskName">
    /// The task name to match. When <c>null</c> (default), matches any task name.
    /// </param>
    public static ConfiguredCall SetupRunAsync(
        this ITaskStateManager manager,
        string? taskName = null)
    {
        var configuredCall = taskName is null
            ? manager.RunAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            : manager.RunAsync(
                taskName,
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>());

        return configuredCall.Returns(async (CallInfo ci) =>
            await ci.Arg<Func<CancellationToken, Task>>()(ci.Arg<CancellationToken>()));
    }
}
