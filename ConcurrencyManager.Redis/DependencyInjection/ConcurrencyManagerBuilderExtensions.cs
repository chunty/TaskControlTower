using ConcurrencyManager.DependencyInjection;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace ConcurrencyManager.Redis.DependencyInjection;

public static class ConcurrencyManagerBuilderExtensions
{
    /// <summary>
    /// Uses a dedicated Redis cache as the backing store, isolated from any other Redis cache
    /// the app may have registered.
    /// </summary>
    /// <example>
    /// services.AddConcurrencyManager()
    ///         .AddRedisStore(o => o.Configuration = "localhost:6379");
    /// </example>
    public static ConcurrencyManagerBuilder AddRedisStore(
        this ConcurrencyManagerBuilder builder,
        Action<RedisCacheOptions> configure)
    {
        return builder.AddDistributedStore(_ =>
        {
            var options = new RedisCacheOptions();
            configure(options);
            return new RedisCache(options);
        });
    }
}
