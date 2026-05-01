using Microsoft.Extensions.DependencyInjection;
using TaskControlTower;
using TaskControlTower.DependencyInjection;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var lockDir = Path.Combine(Path.GetTempPath(), "task-control-tower-demo");
const string TaskName = "nightly-report";

// ── Build the service provider ────────────────────────────────────────────────

var sp = new ServiceCollection()
    .AddTaskControlTower()
    .UseTaskStateStore(_ => new FileTaskStateStore(lockDir))
    .Services
    .BuildServiceProvider();

var manager = sp.GetRequiredService<ITaskStateManager>();
var store   = sp.GetRequiredService<ITaskStateStore>();

// ── Header ────────────────────────────────────────────────────────────────────

Section("TaskControlTower — Persistence Demo");
Info($"Lock directory : {lockDir}");
Info($"Task name      : {TaskName}");
Console.WriteLine();

// ── Show what state was left from the previous run ────────────────────────────

Section("1. State on startup");

var isRunning = await manager.IsRunningAsync(TaskName);
if (isRunning)
{
    Warn($"\"{TaskName}\" is STILL RUNNING — persisted from a previous run ✓");
    Console.WriteLine();
    Info("This proves the file-backed store survived the process exiting.");
    Console.WriteLine();

    // ── Demonstrate crash recovery ────────────────────────────────────────────

    Section("2. Crash recovery");
    Info("Calling store.CleanupAsync() — this is what CleanupOnStartup = true does automatically...");

    await store.CleanupAsync();

    var stillRunning = await manager.IsRunningAsync(TaskName);
    Good($"After cleanup: \"{TaskName}\" running = {stillRunning}");
    Console.WriteLine();
}
else
{
    Good($"\"{TaskName}\" is not running — clean slate");
    Console.WriteLine();
}

// ── Run the task cleanly to show normal operation ─────────────────────────────

Section(isRunning ? "3. Normal run (after recovery)" : "2. Normal run");
Info("Attempting TryRunAsync...");

var ran = await manager.TryRunAsync(TaskName, async ct =>
{
    Info("  Work in progress...");
    await Task.Delay(300, ct);
});

Good(ran
    ? $"\"{TaskName}\" ran and stopped cleanly ✓"
    : $"\"{TaskName}\" skipped — already running");

Console.WriteLine();

// ── Simulate a crash — start but don't stop ───────────────────────────────────

Section(isRunning ? "4. Simulating another crash" : "3. Simulating a crash");
Info("Starting task then exiting WITHOUT calling TryStopAsync...");
Info($"(Lock file will remain at: {Path.Combine(lockDir, TaskName + ".lock")})");
Console.WriteLine();

await manager.StartAsync(TaskName);

Good("Task started. Exiting now — run the app again to see the state has persisted.");
Console.WriteLine();

// Exit immediately without stopping — the lock file stays on disk
Environment.Exit(0);

// ── Helpers ───────────────────────────────────────────────────────────────────

static void Section(string title)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"── {title} {new string('─', Math.Max(0, 60 - title.Length - 4))}");
    Console.ResetColor();
}

static void Info(string msg)  { Console.ForegroundColor = ConsoleColor.Gray;    Console.WriteLine($"   {msg}"); Console.ResetColor(); }
static void Good(string msg)  { Console.ForegroundColor = ConsoleColor.Green;   Console.WriteLine($"   {msg}"); Console.ResetColor(); }
static void Warn(string msg)  { Console.ForegroundColor = ConsoleColor.Yellow;  Console.WriteLine($"   {msg}"); Console.ResetColor(); }
