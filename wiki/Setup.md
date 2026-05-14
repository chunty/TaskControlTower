# Setup

## Installation

```
dotnet add package TaskTurnstile
```

For distributed persistence, also add one of the backing store packages:

```
dotnet add package TaskTurnstile.Redis
dotnet add package TaskTurnstile.SqlServer
```

---

## In-memory (single instance, no persistence)

```csharp
builder.Services.AddTaskTurnstile();
```

The default store is a private in-memory cache — isolated from your app's own `IMemoryCache` and requiring zero configuration.

---

## Redis

```csharp
builder.Services.AddTaskTurnstile()
                .AddRedisStore(o => o.Configuration = "localhost:6379");
```

This creates a **dedicated** Redis connection for TaskTurnstile, independent of any other Redis cache your app may be using. It can point at the same Redis instance as your app or a completely separate one.

If you'd prefer TaskTurnstile to share your app's **existing** `IDistributedCache` instead, use `AddDistributedStore()` (see below).

---

## SQL Server

```csharp
builder.Services.AddTaskTurnstile()
                .AddSqlServerStore(o =>
                {
                    o.ConnectionString = "Server=.;Database=MyApp;...";
                    o.TableName = "ActiveTasks";
                    o.SchemaName = "dbo";
                });
```

The table is created automatically on first startup — no manual migration required.

---

## Shared `IDistributedCache`

If you've already registered a distributed cache (e.g. `AddStackExchangeRedisCache`) and want TaskTurnstile to share it:

```csharp
builder.Services.AddTaskTurnstile()
                .AddDistributedStore();
```

Task keys are prefixed with `KeyPrefix` (default `"cm:"`) to avoid collisions with your own cache entries.

---

## Options

```csharp
builder.Services.AddTaskTurnstile(o =>
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

> **Performance note:** Regardless of which backing store you choose, all state checks are short-circuited by a local in-process memory cache. Once this instance marks a task as running, subsequent checks skip the backing store entirely until the task stops or its `maxRuntime` expires.
