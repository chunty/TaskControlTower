# Task Control Tower

A thread-safe named task lifecycle manager for .NET. Prevents duplicate background job execution across threads and — optionally — across multiple application instances via a distributed backing store.

## Why?

Scheduled jobs (Coravel, Hangfire, Quartz) fire on a timer. If the previous run hasn't finished, you don't want a second one to start. `ConcurrencyManager` gives you a named gate:

```csharp
if (!await _concurrencyManager.CanStartAsync("import-job"))
    return; // already running, skip this tick
```

Unlike a simple lock, the state can survive app restarts (via Redis or SQL Server) and be shared across multiple instances of your app.

---

## Setup

### In-memory (single instance, no persistence)

```csharp
builder.Services.AddConcurrencyManager();
```

The default store is a private in-memory cache — isolated from your app's own `IMemoryCache` and requiring zero configuration.

### Redis

```csharp
builder.Services.AddConcurrencyManager()
                .AddRedisStore(o => o.Configuration = "localhost:6379");
```

This creates a **dedicated** Redis connection for `ConcurrencyManager`, independent of any other Redis cache your app may be using. Configure it with any connection string — it can point at the same Redis instance as your app or a completely separate one.

If you'd prefer `ConcurrencyManager` to share your app's **existing** `IDistributedCache` instead, use `AddDistributedStore()` (see below).

### SQL Server

```csharp
builder.Services.AddConcurrencyManager()
                .AddSqlServerStore(o =>
                {
                    o.ConnectionString = "Server=.;Database=MyApp;...";
                    o.TableName = "ActiveTasks";
                    o.SchemaName = "dbo";
                });
```

> **Note:** The cache table must be created before first use:
> ```
> dotnet sql-cache create "Server=.;Database=MyApp;..." dbo ActiveTasks
> ```

### Use the app's existing `IDistributedCache`

If you've already registered a distributed cache (e.g. `AddStackExchangeRedisCache`) and want `ConcurrencyManager` to share it:

```csharp
builder.Services.AddConcurrencyManager()
                .AddDistributedStore();
```

Task keys are prefixed with `KeyPrefix` (default `"cm:"`) to avoid collisions with your own cache entries. Override it in options if needed.

---

## Options

```csharp
builder.Services.AddConcurrencyManager(o =>
{
    // Maximum time a task can run before it's considered stale.
    // Prevents tasks from being stuck forever if TryStopAsync is never called (e.g. app crash).
    o.DefaultMaxRuntime = TimeSpan.FromHours(2);

    // Remove all running task records on startup.
    // Useful for clearing state left behind by a previous crashed process.
    o.CleanupOnStartup = true;

    // Prefix applied to all keys in the backing store.
    // Change this if "cm:" collides with your own cache keys (only relevant when using AddDistributedStore()).
    o.KeyPrefix = "myapp:tasks:";
});
```

---

## API

Inject `IConcurrencyManager` into your class:

```csharp
public class ImportJob(IConcurrencyManager concurrencyManager)
```

### Check if a task can start

```csharp
bool canStart = await concurrencyManager.CanStartAsync("import-job");
```

Returns `true` if the task is not currently running (or its `maxRuntime` has expired).

### Run with automatic start/stop (recommended)

```csharp
// Returns false immediately if already running; true after work completes.
bool ran = await concurrencyManager.TryRunAsync("import-job", async ct =>
{
    await DoImportAsync(ct);
});
```

```csharp
// With a return value:
var result = await concurrencyManager.TryRunAsync("import-job", async ct =>
{
    return await DoImportAsync(ct);
});

if (result.Started)
    Console.WriteLine($"Imported {result.Value} records");
```

### Wait for a task to be free, then run

```csharp
// Waits until "import-job" is free, then starts and runs the work.
await concurrencyManager.RunAsync("import-job", async ct =>
{
    await DoImportAsync(ct);
});
```

### Manual start/stop

```csharp
if (!await concurrencyManager.StartAsync("import-job"))
    return; // already running

try
{
    await DoImportAsync(cancellationToken);
}
finally
{
    await concurrencyManager.TryStopAsync("import-job");
}
```

### Per-task runtime override

All methods accept an optional `maxRuntime` to override the global default:

```csharp
await concurrencyManager.TryRunAsync("long-job", DoWorkAsync, maxRuntime: TimeSpan.FromHours(4));
```

---

## Real-world patterns

### Coravel invocable (skip if already running)

```csharp
public class ImportInvocable(IConcurrencyManager concurrencyManager) : IInvocable
{
    public async Task Invoke()
    {
        await concurrencyManager.TryRunAsync("import", async ct =>
        {
            await DoImportAsync(ct);
        });
    }
}
```

### BackgroundService (wait for previous run to finish)

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await concurrencyManager.RunAsync("sync", async ct =>
        {
            await DoSyncAsync(ct);
        }, cancellationToken: stoppingToken);

        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
    }
}
```

---

## Custom store

Implement `ITaskStateStore` to back the manager with anything — a file, a database, an API:

```csharp
public class MyCustomStore : ITaskStateStore
{
    public Task<bool> IsRunningAsync(string taskName, CancellationToken ct = default) { ... }
    public Task<bool> IsExpiredAsync(string taskName, CancellationToken ct = default) { ... }
    public Task SetRunningAsync(string taskName, TimeSpan? maxRuntime = null, CancellationToken ct = default) { ... }
    public Task SetStoppedAsync(string taskName, CancellationToken ct = default) { ... }
    public Task CleanupAsync(CancellationToken ct = default) { ... }
}
```

Register it:

```csharp
// Let DI create it:
builder.Services.AddConcurrencyManager()
                .UseTaskStateStore<MyCustomStore>();

// Or use a factory:
builder.Services.AddConcurrencyManager()
                .UseTaskStateStore(sp => new MyCustomStore("/var/run/locks"));
```

See [`samples/FileStore/FileTaskStateStore.cs`](samples/FileStore/FileTaskStateStore.cs) for a complete file-based example.
