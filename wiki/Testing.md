# Testing

## Setup extension methods (recommended for Moq / NSubstitute)

Add [`TaskTurnstile.Testing`](https://www.nuget.org/packages/TaskTurnstile.Testing) to your test project:

```
dotnet add package TaskTurnstile.Testing
```

The methods that take a `Func<CancellationToken, Task>` delegate (`TryRunAsync`, `RunAsync`) require verbose generic type arguments when set up with Moq or NSubstitute. The Testing package provides one-liner extension methods that eliminate that boilerplate while leaving `Verify` / `Received` completely unchanged.

### Moq / AutoMocker

```csharp
using TaskTurnstile.Testing;

// TryRunAsync — runs work and returns true
Mocker.GetMock<ITaskStateManager>().SetupTryRunAsync(returns: true);

// TryRunAsync — skips and returns false
Mocker.GetMock<ITaskStateManager>().SetupTryRunAsync(returns: false);

// RunAsync — runs work
Mocker.GetMock<ITaskStateManager>().SetupRunAsync();

// Generic TryRunAsync<T> — runs work, returns Ran(value)
Mocker.GetMock<ITaskStateManager>().SetupTryRunAsync(value: 42);

// Generic TryRunAsync<T> — skips
Mocker.GetMock<ITaskStateManager>().SetupTryRunAsyncToSkip<int>();
```

### NSubstitute

```csharp
using TaskTurnstile.Testing;

var manager = Substitute.For<ITaskStateManager>();

manager.SetupTryRunAsync(returns: true);
manager.SetupTryRunAsync(returns: false);
manager.SetupRunAsync();
manager.SetupTryRunAsync(value: 42);
manager.SetupTryRunAsyncToSkip<int>();
```

### Matching a specific task name

Pass `taskName` to make the setup match only that name. Any other name falls through to the Moq / NSubstitute default. This is useful when the thing you're testing is *which* name gets passed:

```csharp
// Moq
Mocker.GetMock<ITaskStateManager>().SetupTryRunAsync(returns: true, taskName: "import-job");

// Verify the correct name was used
Mocker.GetMock<ITaskStateManager>()
    .Verify(m => m.TryRunAsync(
        "import-job",
        It.IsAny<Func<CancellationToken, Task>>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<CancellationToken>()), Times.Once);
```

```csharp
// NSubstitute
manager.SetupTryRunAsync(returns: true, taskName: "import-job");

await manager.Received(1).TryRunAsync(
    "import-job",
    Arg.Any<Func<CancellationToken, Task>>(),
    Arg.Any<TimeSpan?>(),
    Arg.Any<CancellationToken>());
```

---

## FakeTaskStateManager

`FakeTaskStateManager` is a framework-agnostic in-memory implementation of `ITaskStateManager`. It executes work inline with no distributed state, no polling, and no boilerplate. Use it when you don't need to verify which task name was passed — it's the simplest option for "does the handler actually do the work" tests.

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

If you prefer to wire up your mocking framework directly without the helper extensions, the key is capturing and invoking the `work` delegate inside the `Returns` callback.

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
        await ci.Arg<Func<CancellationToken, Task>>()(ci.Arg<CancellationToken>());
        return true;
    });
```
