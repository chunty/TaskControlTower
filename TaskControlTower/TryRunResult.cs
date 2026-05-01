namespace TaskControlTower;

public record TryRunResult<T>(bool Started, T? Value)
{
    public static TryRunResult<T> Skipped { get; } = new(false, default);
    public static TryRunResult<T> Ran(T value) => new(true, value);
}
