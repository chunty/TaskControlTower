using TaskTurnstile;
using Microsoft.Extensions.DependencyInjection;

// ──────────────────────────────────────────────────────────────────────────────
// Sample: File-based ITaskStateStore implementation
//
// Each running task is represented by a small JSON file on disk:
//   {directory}/{taskName}.lock
//
// The file contains the expiry time (if any). A task is considered running if
// its lock file exists and hasn't expired. On startup, CleanupAsync removes all
// lock files left behind by a previous crashed process.
//
// NOTE: This is a single-process sample. For true cross-process file locking
// you would need OS-level file locks (FileShare.None + retry) which is omitted
// here for clarity.
// ──────────────────────────────────────────────────────────────────────────────

public sealed class FileTaskStateStore(string directory) : ITaskStateStore
{
    public FileTaskStateStore() : this(Path.Combine(Path.GetTempPath(), "task-turnstile")) { }

    private string LockFile(string taskName) =>
        Path.Combine(directory, $"{taskName}.lock");

    public Task<bool> IsRunningAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var path = LockFile(taskName);
        if (!File.Exists(path))
            return Task.FromResult(false);

        var expiresAt = ReadExpiry(path);
        return Task.FromResult(expiresAt is null || expiresAt > DateTimeOffset.UtcNow);
    }

    public Task<bool> IsExpiredAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var path = LockFile(taskName);
        if (!File.Exists(path))
            return Task.FromResult(false);

        var expiresAt = ReadExpiry(path);
        return Task.FromResult(expiresAt is not null && expiresAt <= DateTimeOffset.UtcNow);
    }

    public Task SetRunningAsync(string taskName, TimeSpan? maxRuntime = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);

        var expiresAt = maxRuntime.HasValue
            ? DateTimeOffset.UtcNow.Add(maxRuntime.Value).ToString("O")
            : string.Empty;

        File.WriteAllText(LockFile(taskName), expiresAt);
        return Task.CompletedTask;
    }

    public Task SetStoppedAsync(string taskName, CancellationToken cancellationToken = default)
    {
        var path = LockFile(taskName);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    public Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
            return Task.CompletedTask;

        foreach (var file in Directory.EnumerateFiles(directory, "*.lock"))
            File.Delete(file);

        return Task.CompletedTask;
    }

    private static DateTimeOffset? ReadExpiry(string path)
    {
        var content = File.ReadAllText(path).Trim();
        return string.IsNullOrEmpty(content) ? null : DateTimeOffset.Parse(content);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// Registration — wire it up via UseTaskStateStore<T>()
// ──────────────────────────────────────────────────────────────────────────────

// services.AddTaskTurnstile(o => o.CleanupOnStartup = true)
//         .UseTaskStateStore<FileTaskStateStore>();
//
// Or with a factory if you need to pass the directory:
//
// services.AddTaskTurnstile(o => o.CleanupOnStartup = true)
//         .UseTaskStateStore(sp => new FileTaskStateStore("/var/run/my-app/locks"));
