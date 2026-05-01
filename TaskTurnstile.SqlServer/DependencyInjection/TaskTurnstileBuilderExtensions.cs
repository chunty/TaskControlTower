using TaskTurnstile.DependencyInjection;
using Microsoft.Extensions.Caching.SqlServer;

namespace TaskTurnstile.SqlServer.DependencyInjection;

public static class TaskTurnstileBuilderExtensions
{
    /// <summary>
    /// Uses a dedicated SQL Server distributed cache as the backing store, isolated from any
    /// other distributed cache the app may have registered.
    /// The cache table is created automatically on first startup if it does not already exist.
    /// </summary>
    /// <example>
    /// services.AddTaskTurnstile()
    ///         .AddSqlServerStore(o =>
    ///         {
    ///             o.ConnectionString = "Server=...";
    ///             o.SchemaName = "dbo";
    ///             o.TableName = "TaskTurnstileCache";
    ///         });
    /// </example>
    public static TaskTurnstileBuilder AddSqlServerStore(
        this TaskTurnstileBuilder builder,
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
