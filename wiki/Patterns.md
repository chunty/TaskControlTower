# Patterns

## Coravel invocable — skip if already running

The most common pattern: use `TryRunAsync` so that if the previous execution is still in progress, the new invocation is silently skipped.

```csharp
public class ImportInvocable(ITaskStateManager manager) : IInvocable
{
    public async Task Invoke()
    {
        await manager.TryRunAsync("import", async ct =>
        {
            await DoImportAsync(ct);
        });
    }
}
```

---

## BackgroundService — wait for previous run to finish

Use `RunAsync` when you want each iteration to run to completion before starting the next, even if the scheduled interval has already passed.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await manager.RunAsync("sync", async ct =>
        {
            await DoSyncAsync(ct);
        }, cancellationToken: stoppingToken);

        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
    }
}
```

---

## Manual start/stop

Use `StartAsync` / `TryStopAsync` when you need fine-grained control — for example, when work spans multiple methods or is managed outside a single `async` scope.

```csharp
if (!await manager.StartAsync("import-job"))
    return; // already running

try
{
    await DoImportAsync(cancellationToken);
}
finally
{
    await manager.TryStopAsync("import-job");
}
```

---

## Returning a value from TryRunAsync

```csharp
var result = await manager.TryRunAsync("report-job", async ct =>
{
    return await GenerateReportAsync(ct);
});

if (result.Started)
    await SendReportAsync(result.Value);
else
    logger.LogInformation("Report job already running — skipped.");
```
