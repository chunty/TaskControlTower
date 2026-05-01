using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using System.Data;

namespace TaskTurnstile.SqlServer;

internal sealed class SqlServerTableInitializerHostedService(
    string connectionString,
    string schemaName,
    string tableName) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var quotedTable = $"[{schemaName.Replace("]", "]]")}].[{tableName.Replace("]", "]]")}]";
        var escapedSchema = schemaName.Replace("'", "''");
        var escapedTable = tableName.Replace("'", "''");

        var tableInfoSql =
            $"SELECT 1 FROM INFORMATION_SCHEMA.TABLES " +
            $"WHERE TABLE_SCHEMA = '{escapedSchema}' AND TABLE_NAME = '{escapedTable}'";

        var createTableSql =
            $"CREATE TABLE {quotedTable}(" +
            "Id nvarchar(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL, " +
            "Value varbinary(MAX) NOT NULL, " +
            "ExpiresAtTime datetimeoffset NOT NULL, " +
            "SlidingExpirationInSeconds bigint NULL, " +
            "AbsoluteExpiration datetimeoffset NULL, " +
            "PRIMARY KEY (Id))";

        var createIndexSql =
            $"CREATE NONCLUSTERED INDEX Index_ExpiresAtTime ON {quotedTable}(ExpiresAtTime)";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var checkCommand = new SqlCommand(tableInfoSql, connection);
        await using var reader = await checkCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        var exists = await reader.ReadAsync(cancellationToken);
        await reader.CloseAsync();

        if (exists)
            return;

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await new SqlCommand(createTableSql, connection, transaction).ExecuteNonQueryAsync(cancellationToken);
            await new SqlCommand(createIndexSql, connection, transaction).ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
