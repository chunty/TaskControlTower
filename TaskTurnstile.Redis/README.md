# TaskTurnstile.Redis

Redis backing store for [TaskTurnstile](https://www.nuget.org/packages/TaskTurnstile). Persists task state across app restarts and multiple instances.

```csharp
builder.Services.AddTaskTurnstile()
                .AddRedisStore(o => o.Configuration = "localhost:6379");
```

This creates a **dedicated** Redis connection for TaskTurnstile, independent of any other Redis cache your app uses.

For full documentation and samples see the [GitHub repository](https://github.com/chunty/TaskTurnstile).
