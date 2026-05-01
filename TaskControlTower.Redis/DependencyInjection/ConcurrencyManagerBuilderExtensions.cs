using TaskControlTower.DependencyInjection;
using Microsoft.Extensions.Caching.StackExchangeRedis;

namespace TaskControlTower.Redis.DependencyInjection;

public static class TaskControlTowerBuilderExtensions
{
    /// <summary>
    /// Uses a dedicated Redis cache as the backing store, isolated from any other Redis cache
    /// the app may have registered.
    /// </summary>
    /// <example>
    /// services.AddTaskControlTower()
    ///         .AddRedisStore(o => o.Configuration = "localhost:6379");
    /// </example>
    public static TaskControlTowerBuilder AddRedisStore(
        this TaskControlTowerBuilder builder,
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
