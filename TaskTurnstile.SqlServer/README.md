# TaskTurnstile.SqlServer

SQL Server backing store for [TaskTurnstile](https://www.nuget.org/packages/TaskTurnstile). Persists task state across app restarts and multiple instances. The cache table is created automatically on first startup.

```csharp
builder.Services.AddTaskTurnstile()
                .AddSqlServerStore(o =>
                {
                    o.ConnectionString = "Server=.;Database=MyApp;...";
                    o.TableName = "ActiveTasks";
                    o.SchemaName = "dbo";
                });
```

For full documentation and samples see the [GitHub repository](https://github.com/chunty/TaskTurnstile).
