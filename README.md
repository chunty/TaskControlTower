# Task Turnstile
[![NuGet](https://img.shields.io/nuget/v/TaskTurnstile.svg)](https://www.nuget.org/packages/TaskTurnstile)
[![NuGet Downloads](https://img.shields.io/nuget/dt/TaskTurnstile.svg)](https://www.nuget.org/packages/TaskTurnstile)
[![CI](https://github.com/chunty/TaskTurnstile/actions/workflows/ci.yml/badge.svg)](https://github.com/chunty/TaskTurnstile/actions/workflows/ci.yml)
[![Publish Wiki](https://github.com/chunty/TaskTurnstile/actions/workflows/publish-wiki.yml/badge.svg)](https://github.com/chunty/TaskTurnstile/actions/workflows/publish-wiki.yml)

Prevents duplicate background job execution in .NET — within a single instance **and across multiple instances** via a distributed backing store. Perfect for scheduled jobs running on multiple pods, servers, or worker processes where only one should run at a time.

> **Think of it like a turnstile.** Every job that wants to run must push through first. Only one can hold the bar at a time — others wait their turn or are sent away. When the job is done, the bar rotates and the next one can step through.

## Why?

Scheduled jobs (Coravel, Hangfire, Quartz) fire on a timer. If the previous run hasn't finished, you don't want a second one to start. `TaskTurnstile` gives you a named gate:

```csharp
if (!await manager.CanStartAsync("import-job"))
    return; // already running, skip this tick
```

Unlike a simple lock, the state can survive app restarts (via Redis or SQL Server) and be shared across multiple instances of your app.

## Install

```
dotnet add package TaskTurnstile
```

## Quick start

```csharp
builder.Services.AddTaskTurnstile();
```

```csharp
bool ran = await manager.TryRunAsync("import-job", async ct =>
{
    await DoImportAsync(ct);
});
```

Returns `true` after the work completes, or `false` immediately if the task is already running.

## Scale out across instances

The default store is in-memory — great for a single instance. Add a backing store to prevent duplicate runs across **multiple pods, servers, or worker processes**:

**Redis**
```csharp
builder.Services.AddTaskTurnstile()
                .AddRedisStore(o => o.Configuration = "localhost:6379");
```

**SQL Server**
```csharp
builder.Services.AddTaskTurnstile()
                .AddSqlServerStore(o => o.ConnectionString = "Server=.;Database=MyApp;...");
```

**Shared `IDistributedCache`** (use your app's existing distributed cache)
```csharp
builder.Services.AddTaskTurnstile()
                .AddDistributedStore();
```

> All three options use an in-process memory short-circuit — the backing store is only hit when a task isn't already known to be running locally, keeping overhead minimal. See [Setup](https://github.com/chunty/TaskTurnstile/wiki/Setup) for full options.

## Documentation

| | |
|---|---|
| [Setup](https://github.com/chunty/TaskTurnstile/wiki/Setup) | In-memory, Redis, SQL Server, shared `IDistributedCache`, and all options |
| [API Reference](https://github.com/chunty/TaskTurnstile/wiki/API-Reference) | Full `ITaskStateManager` method reference |
| [Patterns](https://github.com/chunty/TaskTurnstile/wiki/Patterns) | Coravel, BackgroundService, manual start/stop, returning values |
| [Testing](https://github.com/chunty/TaskTurnstile/wiki/Testing) | `FakeTaskStateManager` and manual Moq / NSubstitute setup |
| [Custom Store](https://github.com/chunty/TaskTurnstile/wiki/Custom-Store) | Implement `ITaskStateStore` to use any backing store |

## Packages

| Package | NuGet | Purpose |
|---|---|---|
| `TaskTurnstile` | [![NuGet](https://img.shields.io/nuget/v/TaskTurnstile.svg)](https://www.nuget.org/packages/TaskTurnstile) | Core package |
| `TaskTurnstile.Redis` | [![NuGet](https://img.shields.io/nuget/v/TaskTurnstile.Redis.svg)](https://www.nuget.org/packages/TaskTurnstile.Redis) | Dedicated Redis backing store |
| `TaskTurnstile.SqlServer` | [![NuGet](https://img.shields.io/nuget/v/TaskTurnstile.SqlServer.svg)](https://www.nuget.org/packages/TaskTurnstile.SqlServer) | SQL Server backing store |
| `TaskTurnstile.Testing` | [![NuGet](https://img.shields.io/nuget/v/TaskTurnstile.Testing.svg)](https://www.nuget.org/packages/TaskTurnstile.Testing) | `FakeTaskStateManager` test double |

