# TaskTurnstile.Testing

Setup extension methods for [TaskTurnstile](https://www.nuget.org/packages/TaskTurnstile) that eliminate boilerplate when mocking `ITaskStateManager` with Moq or NSubstitute.

```csharp
// Before
Mocker.GetMock<ITaskStateManager>()
    .Setup(m => m.TryRunAsync(
        It.IsAny<string>(),
        It.IsAny<Func<CancellationToken, Task>>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<CancellationToken>()))
    .Returns<string, Func<CancellationToken, Task>, TimeSpan?, CancellationToken>(
        async (_, work, _, ct) => { await work(ct); return true; });

// After
Mocker.GetMock<ITaskStateManager>().SetupTryRunAsync(returns: true);
```

For full documentation and samples see the [GitHub repository](https://github.com/chunty/TaskTurnstile).
