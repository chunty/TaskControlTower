namespace TaskControlTower;

public sealed class TaskControlTowerOptions
{
    /// <summary>Default MaxRuntime applied when StartAsync is called without an explicit maxRuntime. Null means no expiry.</summary>
    public TimeSpan? DefaultMaxRuntime { get; set; }

    /// <summary>When true, registers an IHostedService that calls CleanupAsync on startup to clear tasks left running from a previous crash.</summary>
    public bool CleanupOnStartup { get; set; }

    /// <summary>
    /// Prefix applied to all task keys in the backing store.
    /// Useful when using AddDistributedStore() to share your app's IDistributedCache, to avoid collisions with your own cache keys.
    /// Defaults to "cm:".
    /// </summary>
    public string KeyPrefix { get; set; } = "cm:";
}
