using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using TaskTurnstile.DependencyInjection;
using TaskTurnstile.SqlServer.DependencyInjection;

namespace TaskTurnstile.Tests.Integration;

/// <summary>
/// Proves that AddSqlServerStore auto-creates the cache table against a real LocalDB instance.
/// Requires: sqllocaldb start MSSQLLocalDB
/// Each test instance uses its own uniquely-named database so tests are safe to run in parallel.
/// </summary>
public class SqlServerTableCreationIntegrationTests : IAsyncLifetime
{
    private readonly string _dbName = $"TaskTurnstileTest_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $@"Server=(localdb)\MSSQLLocalDB;Database={_dbName};Integrated Security=true;TrustServerCertificate=true;";

    private static string MasterConnectionString =>
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=true;TrustServerCertificate=true;";

    public async ValueTask InitializeAsync()
    {
        await using var connection = new SqlConnection(MasterConnectionString);
        await connection.OpenAsync();
        await new SqlCommand($"CREATE DATABASE [{_dbName}]", connection).ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
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
