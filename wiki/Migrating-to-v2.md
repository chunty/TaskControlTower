# Migrating to v2.0

## Breaking changes

### `ITaskStateManager` — `string taskName` → `object taskKey`

All nine methods on `ITaskStateManager` have changed their first parameter from `string taskName` to `object taskKey`.

**Before (v1.x):**

```csharp
await manager.TryRunAsync("import-job", async ct => { ... });
await manager.RunAsync("import-job", async ct => { ... });
await manager.CanStartAsync("import-job");
await manager.StartAsync("import-job");
await manager.TryStopAsync("import-job");
await manager.IsRunningAsync("import-job");
await manager.WaitAsync("import-job", cancellationToken);
```

**After (v2.0):**

```csharp
await manager.TryRunAsync("import-job", async ct => { ... }); // strings still work as-is
await manager.TryRunAsync(42, async ct => { ... });            // primitives now supported
await manager.TryRunAsync(new JobKey { TenantId = 1 }, async ct => { ... }); // objects supported
```

String keys continue to work with no code changes required — they pass through unchanged. The parameter rename (`taskName` → `taskKey`) only requires changes if you were using named arguments:

```csharp
// Before
await manager.TryRunAsync(taskName: "import-job", work: DoWorkAsync);

// After
await manager.TryRunAsync(taskKey: "import-job", work: DoWorkAsync);
```

### Key format for non-string types

If you were using the intermediate pre-release `object taskKey` API and stored records in your database, note that primitive/enum/Guid/DateTime keys are now stored as `{TypeFullName}:{value}` (e.g. `System.Int32:42`) rather than just the raw value. Existing string-keyed records are unaffected.

### `TaskStateManagerExtensions` removed

The `TryRunAsync(string taskName, ...)` and `RunAsync(string taskName, ...)` generic overloads that previously lived as extension methods are no longer needed — the interface methods accept `object taskKey` directly.

If you were calling these extension methods they will need updating to call the interface methods directly, which have the same signatures.

---

## What's new in v2.0

- **Object task keys** — pass any type as a task key. Strings are unchanged; primitives, enums, `Guid`, `DateTime`, and other value types use `ToString()` prefixed with the type name; complex objects are JSON-serialised and SHA-256 hashed.
- Keys are always human-readable in the database (type name prefix makes them identifiable).
