using TaskTurnstile.DependencyInjection;
using TaskTurnstile.SqlServer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace TaskTurnstile.Tests.DependencyInjection;

public class SqlServerTaskTurnstileBuilderExtensionsTests
{
    // Minimal config — only ConnectionString set; SchemaName and TableName use defaults.
    private static TaskTurnstileBuilder MinimalBuilder() =>
        new ServiceCollection()
            .AddTaskTurnstile()
            .AddSqlServerStore(o => o.ConnectionString = "Server=.;Database=Test;");

    [Fact]
    public void AddSqlServerStore_MinimalConfig_ReturnsBuilder()
    {
        Assert.NotNull(MinimalBuilder());
    }

    [Fact]
    public void AddSqlServerStore_NullConnectionString_ThrowsAtRegistration()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection()
                .AddTaskTurnstile()
                .AddSqlServerStore(o => { /* ConnectionString intentionally not set */ }));
    }

    [Fact]
    public void AddSqlServerStore_ResolvingStore_AttemptsTableInitialization()
    {
        // With no real SQL Server available it will throw a connection error — confirming
        // EnsureTableExistsAsync is wired up and not silently skipped.
        var sp = new ServiceCollection()
            .AddTaskTurnstile()
            .AddSqlServerStore(o => o.ConnectionString = "Server=nonexistent;Database=Test;Connect Timeout=1;")
            .Services
            .BuildServiceProvider();

        Assert.ThrowsAny<Exception>(() => sp.GetRequiredService<ITaskStateStore>());
    }
}
