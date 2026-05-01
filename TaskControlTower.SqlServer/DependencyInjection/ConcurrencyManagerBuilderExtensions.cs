using TaskControlTower.DependencyInjection;
using Microsoft.Extensions.Caching.SqlServer;

namespace TaskControlTower.SqlServer.DependencyInjection;

public static class TaskControlTowerBuilderExtensions
{
    /// <summary>
    /// Uses a dedicated SQL Server distributed cache as the backing store, isolated from any
    /// other distributed cache the app may have registered.
    /// The cache table must exist — create it with: dotnet sql-cache create "connection" schema table
    /// </summary>
    /// <example>
    /// services.AddTaskControlTower()
    ///         .AddSqlServerStore(o =>
    ///         {
    ///             o.ConnectionString = "Server=...";
    ///             o.SchemaName = "dbo";
    ///             o.TableName = "TaskControlTowerCache";
    ///         });
    /// </example>
    public static TaskControlTowerBuilder AddSqlServerStore(
        this TaskControlTowerBuilder builder,
        Action<SqlServerCacheOptions> configure)
    {
        return builder.AddDistributedStore(_ =>
        {
            var options = new SqlServerCacheOptions();
            configure(options);
            return new SqlServerCache(options);
        });
    }
}
