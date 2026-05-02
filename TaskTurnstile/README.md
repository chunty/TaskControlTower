# TaskTurnstile

A thread-safe named task lifecycle manager for .NET. Prevents duplicate background job execution across threads and — optionally — across multiple instances via a distributed backing store.

```csharp
builder.Services.AddTaskTurnstile();
```

```csharp
bool ran = await manager.TryRunAsync("import-job", async ct =>
{
    await DoImportAsync(ct);
});
```

For distributed persistence add [`TaskTurnstile.Redis`](https://www.nuget.org/packages/TaskTurnstile.Redis) or [`TaskTurnstile.SqlServer`](https://www.nuget.org/packages/TaskTurnstile.SqlServer).

For full documentation and samples see the [GitHub repository](https://github.com/chunty/TaskTurnstile).
