# Testing

## Setup extension methods (recommended for Moq / NSubstitute)

Add [`TaskTurnstile.Testing`](https://www.nuget.org/packages/TaskTurnstile.Testing) to your test project:

```
dotnet add package TaskTurnstile.Testing
```

The methods that take a `Func<CancellationToken, Task>` delegate (`TryRunAsync`, `RunAsync`) require verbose generic type arguments when set up with Moq or NSubstitute. The Testing package provides one-liner extension methods that eliminate that boilerplate while leaving `Verify` / `Received` completely unchanged.

### Moq / AutoMocker

```csharp
using TaskTurnstile.Testing.Moq;

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
using TaskTurnstile.Testing.NSubstitute;

var manager = Substitute.For<ITaskStateManager>();

manager.SetupTryRunAsync(returns: true);
manager.SetupTryRunAsync(returns: false);
manager.SetupRunAsync();
manager.SetupTryRunAsync(value: 42);
manager.SetupTryRunAsyncToSkip<int>();
```

### Matching a specific task key

Pass `taskKey` to make the setup match only that key. Any other key falls through to the Moq / NSubstitute default. This is useful when the thing you're testing is *which* key gets passed:

```csharp
// Moq — string key
Mocker.GetMock<ITaskStateManager>().SetupTryRunAsync(returns: true, taskKey: "import-job");

// Moq — object key
Mocker.GetMock<ITaskStateManager>().SetupTryRunAsync(returns: true, taskKey: new JobKey { TenantId = 42 });

// Verify the correct key was used
Mocker.GetMock<ITaskStateManager>()
    .Verify(m => m.TryRunAsync(
        "import-job",
        It.IsAny<Func<CancellationToken, Task>>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<CancellationToken>()), Times.Once);
```

```csharp
// NSubstitute
manager.SetupTryRunAsync(returns: true, taskKey: "import-job");

await manager.Received(1).TryRunAsync(
    "import-job",
    Arg.Any<Func<CancellationToken, Task>>(),
    Arg.Any<TimeSpan?>(),
    Arg.Any<CancellationToken>());
```

### Resolving object keys in store-level assertions

When asserting on `ITaskStateStore` calls directly (e.g., in custom store tests), you need the resolved string key. Use `TaskKeyConverter.ToKey` to compute it:

```csharp
using TaskTurnstile;

var key = new JobKey { TenantId = 42 };
var storeKey = TaskKeyConverter.ToKey(key); // e.g. "MyApp.JobKey:a3f9..."

storeMock.Verify(s => s.SetRunningAsync(storeKey, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()));
```

For string and primitive keys:

```csharp
TaskKeyConverter.ToKey("import-job")  // → "import-job"
TaskKeyConverter.ToKey(42)            // → "System.Int32:42"
```

---

## Manual mocking (without TaskTurnstile.Testing)

If you prefer to wire up your mocking framework directly without the helper extensions, the key is capturing and invoking the `work` delegate inside the `Returns` callback.

### Moq / AutoMocker — TryRunAsync runs work, returns `true`

```csharp
Mocker.GetMock<ITaskStateManager>()
    .Setup(m => m.TryRunAsync(
        It.IsAny<object>(),
        It.IsAny<Func<CancellationToken, Task>>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<CancellationToken>()))
    .Returns<object, Func<CancellationToken, Task>, TimeSpan?, CancellationToken>(
        async (_, work, _, ct) => { await work(ct); return true; });
```

### Moq / AutoMocker — TryRunAsync skips, returns `false`

```csharp
Mocker.GetMock<ITaskStateManager>()
    .Setup(m => m.TryRunAsync(
        It.IsAny<object>(),
        It.IsAny<Func<CancellationToken, Task>>(),
        It.IsAny<TimeSpan?>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(false);
```

### NSubstitute — TryRunAsync runs work, returns `true`

```csharp
manager.TryRunAsync(
        Arg.Any<object>(),
        Arg.Any<Func<CancellationToken, Task>>(),
        Arg.Any<TimeSpan?>(),
        Arg.Any<CancellationToken>())
    .Returns(async ci =>
    {
        await ci.Arg<Func<CancellationToken, Task>>()(ci.Arg<CancellationToken>());
        return true;
    });
```
