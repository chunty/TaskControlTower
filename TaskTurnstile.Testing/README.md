# TaskTurnstile.Testing

Test double for [TaskTurnstile](https://www.nuget.org/packages/TaskTurnstile). Provides `FakeTaskStateManager` — a framework-agnostic in-memory implementation of `ITaskStateManager`. No mocking framework required.

```csharp
// AutoMocker (Moq)
Mocker.Use<ITaskStateManager>(new FakeTaskStateManager());

// NSubstitute / plain injection
var fake = new FakeTaskStateManager();

// Simulate "task already running" for testing the skip path
var fake = new FakeTaskStateManager();
fake.MarkRunning("my-job");
```

For full documentation and samples see the [GitHub repository](https://github.com/chunty/TaskTurnstile).
