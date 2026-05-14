# API Reference

Inject `ITaskStateManager` into your class:

```csharp
public class ImportJob(ITaskStateManager manager) { ... }
```

---

## TryRunAsync — run or skip

The recommended method for most scenarios. Starts the task, runs the work, and stops it. Returns `false` immediately if the task is already running.

```csharp
// Returns false if already running; true after work completes.
bool ran = await manager.TryRunAsync("import-job", async ct =>
{
    await DoImportAsync(ct);
});
```

**With a return value:**

```csharp
var result = await manager.TryRunAsync("import-job", async ct =>
{
    return await DoImportAsync(ct);
});

if (result.Started)
    Console.WriteLine($"Imported {result.Value} records");
// result.Started == false means the task was already running (work was skipped)
```

---

## RunAsync — wait then run

Waits (polls) until the named task is free, then starts it, runs the work, and stops it. Throws `OperationCanceledException` if the cancellation token is cancelled while waiting.

```csharp
await manager.RunAsync("import-job", async ct =>
{
    await DoImportAsync(ct);
}, cancellationToken: stoppingToken);
```

---

## CanStartAsync

Returns `true` if the task is not currently running, or if it is running but its `maxRuntime` has elapsed (considered stale).

```csharp
if (!await manager.CanStartAsync("import-job"))
    return; // already running
```

---

## WaitAsync

Waits (polls) until the task can start, then marks it as started. Does **not** run any work — you are responsible for calling `TryStopAsync` when done.

```csharp
// Overload 1: uses default poll interval, no timeout
await manager.WaitAsync("import-job", cancellationToken);

// Overload 2: explicit poll interval and optional max wait
await manager.WaitAsync("import-job", pollIntervalMs: 250, maxWaitMs: 5000, cancellationToken);
// Throws TimeoutException if maxWaitMs is exceeded
```

---

## StartAsync

Marks a task as started. Returns `false` if it is already running.

```csharp
if (!await manager.StartAsync("import-job"))
    return; // already running
```

Accepts an optional `maxRuntime` to override the global default for this specific start:

```csharp
await manager.StartAsync("long-job", maxRuntime: TimeSpan.FromHours(4));
```

---

## TryStopAsync

Marks a task as stopped. Always completes — ignores cancellation to ensure cleanup runs. Returns `false` only if the backing store throws.

```csharp
await manager.TryStopAsync("import-job");
```

---

## IsRunningAsync

Raw data query — returns `true` if a record exists in the store, regardless of whether `maxRuntime` has elapsed. Use for observational purposes only; use `CanStartAsync` for start decisions.

```csharp
bool isRunning = await manager.IsRunningAsync("import-job");
```

---

## Per-call maxRuntime override

All methods that start a task accept an optional `maxRuntime` to override the configured `DefaultMaxRuntime`:

```csharp
await manager.TryRunAsync("long-job", DoWorkAsync, maxRuntime: TimeSpan.FromHours(4));
await manager.StartAsync("long-job", maxRuntime: TimeSpan.FromHours(4));
```
