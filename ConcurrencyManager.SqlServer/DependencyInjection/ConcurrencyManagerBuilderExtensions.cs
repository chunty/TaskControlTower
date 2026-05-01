using ConcurrencyManager.DependencyInjection;
using Microsoft.Extensions.Caching.SqlServer;

namespace ConcurrencyManager.SqlServer.DependencyInjection;

public static class ConcurrencyManagerBuilderExtensions
{
    /// <summary>
    /// Uses a dedicated SQL Server distributed cache as the backing store, isolated from any
    /// other distributed cache the app may have registered.
    /// The cache table must exist — create it with: dotnet sql-cache create "connection" schema table
    /// </summary>
    /// <example>
    /// services.AddConcurrencyManager()
    ///         .AddSqlServerStore(o =>
    ///         {
    ///             o.ConnectionString = "Server=...";
    ///             o.SchemaName = "dbo";
    ///             o.TableName = "ConcurrencyManagerCache";
    ///         });
    /// </example>
    public static ConcurrencyManagerBuilder AddSqlServerStore(
        this ConcurrencyManagerBuilder builder,
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
