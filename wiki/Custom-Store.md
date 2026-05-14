# Custom Store

Implement `ITaskStateStore` to back the manager with anything — a file, a database, an external API.

## Interface

```csharp
public interface ITaskStateStore
{
    Task<bool> IsRunningAsync(string taskName, CancellationToken ct = default);
    Task<bool> IsExpiredAsync(string taskName, CancellationToken ct = default);
    Task SetRunningAsync(string taskName, TimeSpan? maxRuntime = null, CancellationToken ct = default);
    Task SetStoppedAsync(string taskName, CancellationToken ct = default);
    Task CleanupAsync(CancellationToken ct = default);
}
```

| Method | Responsibility |
|---|---|
| `IsRunningAsync` | Returns `true` if a record exists for `taskName` |
| `IsExpiredAsync` | Returns `true` if the record exists but its `maxRuntime` has elapsed |
| `SetRunningAsync` | Creates or updates the record, optionally with an expiry |
| `SetStoppedAsync` | Removes the record |
| `CleanupAsync` | Removes all records — called on startup when `CleanupOnStartup = true` |

## Registration

```csharp
// Let DI create the store:
builder.Services.AddTaskTurnstile()
                .UseTaskStateStore<MyCustomStore>();

// Or use a factory:
builder.Services.AddTaskTurnstile()
                .UseTaskStateStore(sp => new MyCustomStore("/var/run/locks"));
```

## Example

See [`samples/FileStore/FileTaskStateStore.cs`](https://github.com/chunty/TaskTurnstile/blob/main/samples/FileStore/FileTaskStateStore.cs) for a complete file-based implementation.
