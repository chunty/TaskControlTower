# Testing

## FakeTaskStateManager (recommended)

Add [`TaskTurnstile.Testing`](https://www.nuget.org/packages/TaskTurnstile.Testing) to your test project:

```
dotnet add package TaskTurnstile.Testing
```

`FakeTaskStateManager` is a framework-agnostic in-memory implementation of `ITaskStateManager`. It executes work inline with no distributed state, no polling, and no boilerplate.

### Works with any mocking framework

```csharp
// AutoMocker (Moq) — inject the fake instead of setting up a mock
Mocker.Use<ITaskStateManager>(new FakeTaskStateManager());

// NSubstitute / plain injection
var fake = new FakeTaskStateManager();
```

### Testing the "task already running" path

Use `MarkRunning` in your arrange step to simulate a task that is already in progress:

```csharp
var fake = new FakeTaskStateManager();
fake.MarkRunning("my-job");

var ran = await fake.TryRunAsync("my-job", _ => { /* never called */ return Task.CompletedTask; });
Assert.False(ran);
```

### RunAsync / WaitAsync with a pre-marked task

If `RunAsync` or `WaitAsync` is called while a task is already running, the fake spins until it is released (or `WaitTimeout` is exceeded — default 5 seconds). To test the "wait until free" scenario, release the task from a background task:

```csharp
var fake = new FakeTaskStateManager();
fake.MarkRunning("my-job");

_ = Task.Run(async () =>
{
    await Task.Delay(50);
    await fake.TryStopAsync("my-job");
});

await fake.RunAsync("my-job", ct => DoWorkAsync(ct));
```

Adjust the timeout if needed:

```csharp
var fake = new FakeTaskStateManager { WaitTimeout = TimeSpan.FromSeconds(10) };
```

---

## Manual mocking (without TaskTurnstile.Testing)

If you prefer to wire up your mocking framework directly, the key is capturing and invoking the `work` delegate inside the `Returns` callback.

### Moq / AutoMocker — TryRunAsync runs work, returns `true`

```csharp
Mocker.GetMock<ITaskStateManager>()
    .Setup(m => m.TryRunAsync(
        It.IsAny<string>(),
        It.IsAny<Func<CancellationToken, Task>>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<CancellationToken>()))
    .Returns<string, Func<CancellationToken, Task>, TimeSpan?, CancellationToken>(
        async (_, work, _, ct) => { await work(ct); return true; });
```

### Moq / AutoMocker — TryRunAsync skips, returns `false`

```csharp
Mocker.GetMock<ITaskStateManager>()
    .Setup(m => m.TryRunAsync(
        It.IsAny<string>(),
        It.IsAny<Func<CancellationToken, Task>>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(false);
```

### NSubstitute — TryRunAsync runs work, returns `true`

```csharp
manager.TryRunAsync(
        Arg.Any<string>(),
        Arg.Any<Func<CancellationToken, Task>>(),
        Arg.Any<TimeSpan?>(),
        Arg.Any<CancellationToken>())
    .Returns(async ci =>
    {
        await ci.Arg<Func<CancellationToken, Task>>()(CancellationToken.None);
        return true;
    });
```
