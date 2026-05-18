using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using TaskTurnstile.DependencyInjection;
using TaskTurnstile.SqlServer.DependencyInjection;

namespace TaskTurnstile.Tests.Integration;

/// <summary>
/// Proves that AddSqlServerStore auto-creates the cache table against a real SQL Server instance.
/// On Windows (local dev): uses LocalDB — requires sqllocaldb start MSSQLLocalDB.
/// In CI (Linux): uses the SQL Server service container via MSSQL_SA_PASSWORD env var.
/// Each test instance uses its own uniquely-named database so tests are safe to run in parallel.
/// </summary>
public class SqlServerTableCreationIntegrationTests : IAsyncLifetime
{
    private readonly string _dbName = $"TaskTurnstileTest_{Guid.NewGuid():N}";
    private bool _initialized;

    // When MSSQL_SA_PASSWORD is set (CI), use SQL Server container; otherwise fall back to LocalDB.
    private static readonly string? SaPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
    private static readonly bool UseLocalDb = string.IsNullOrEmpty(SaPassword);

    private string ConnectionString =>
        UseLocalDb
            ? $@"Server=(localdb)\MSSQLLocalDB;Database={_dbName};Integrated Security=true;TrustServerCertificate=true;"
            : $"Server=localhost,1433;Database={_dbName};User Id=sa;Password={SaPassword};TrustServerCertificate=true;";

    private static string MasterConnectionString =>
        UseLocalDb
            ? @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;"
            : $"Server=localhost,1433;Database=master;User Id=sa;Password={SaPassword};TrustServerCertificate=true;";

    public async ValueTask InitializeAsync()
    {
        if (UseLocalDb && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Assert.Skip("LocalDB is only available on Windows. Set MSSQL_SA_PASSWORD to run against a SQL Server container.");

        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await new SqlCommand($"CREATE DATABASE [{_dbName}]", connection).ExecuteNonQueryAsync();
        _initialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_initialized) return;
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await new SqlCommand(
            $"ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]",
            connection).ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task AddSqlServerStore_CreatesTableAutomatically()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddTaskTurnstile()
            .AddSqlServerStore(o =>
            {
                o.ConnectionString = ConnectionString;
                // TableName intentionally omitted — should default to "ActiveTasks"
            })
            .Services
            .BuildServiceProvider();

        // Act — resolving ITaskStateStore triggers EnsureTableExistsAsync
        _ = sp.GetRequiredService<ITaskStateStore>();

        // Assert — verify the table was created
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES " +
            "WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'ActiveTasks'",
            connection);

        var count = (int)await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken)!;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AddSqlServerStore_TableAlreadyExists_DoesNotThrow()
    {
        var sp = new ServiceCollection()
            .AddTaskTurnstile()
            .AddSqlServerStore(o => o.ConnectionString = ConnectionString)
            .Services
            .BuildServiceProvider();

        // Resolve twice — second time table already exists, should be a no-op
        _ = sp.GetRequiredService<ITaskStateStore>();
        var ex = Record.Exception(() => sp.GetRequiredService<ITaskStateStore>());

        Assert.Null(ex);
        await Task.CompletedTask;
    }
}
