using ConcurrencyManager.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConcurrencyManager.DependencyInjection;

public sealed class ConcurrencyManagerBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Uses the app's registered <see cref="IDistributedCache"/> as the backing store.
    /// The app must register a distributed cache (e.g. AddStackExchangeRedisCache) before building the container.
    /// Task keys are prefixed with <see cref="ConcurrencyManagerOptions.KeyPrefix"/> (default "cm:") to avoid collisions.
    /// </summary>
    public ConcurrencyManagerBuilder AddDistributedStore()
    {
        ReplaceStore(services => services.AddSingleton<ITaskStateStore>(sp =>
        {
            var opts = sp.GetRequiredService<ConcurrencyManagerOptions>();
            return new DistributedCacheTaskStateStore(
                sp.GetRequiredService<IDistributedCache>(),
                opts.KeyPrefix);
        }));
        return this;
    }

    /// <summary>
    /// Uses a dedicated <see cref="IDistributedCache"/> instance created by the provided factory,
    /// completely isolated from the app's own cache.
    /// Task keys are prefixed with <see cref="ConcurrencyManagerOptions.KeyPrefix"/> (default "cm:").
    /// </summary>
    public ConcurrencyManagerBuilder AddDistributedStore(Func<IServiceProvider, IDistributedCache> cacheFactory)
    {
        ReplaceStore(services => services.AddSingleton<ITaskStateStore>(sp =>
        {
            var opts = sp.GetRequiredService<ConcurrencyManagerOptions>();
            return new DistributedCacheTaskStateStore(cacheFactory(sp), opts.KeyPrefix);
        }));
        return this;
    }

    /// <summary>Replaces the backing store with a custom <see cref="ITaskStateStore"/> implementation.</summary>
    public ConcurrencyManagerBuilder UseTaskStateStore<T>() where T : class, ITaskStateStore
    {
        ReplaceStore(services => services.AddSingleton<ITaskStateStore, T>());
        return this;
    }

    /// <summary>Replaces the backing store with a custom <see cref="ITaskStateStore"/> instance created by the provided factory.</summary>
    public ConcurrencyManagerBuilder UseTaskStateStore(Func<IServiceProvider, ITaskStateStore> factory)
    {
        ReplaceStore(services => services.AddSingleton<ITaskStateStore>(factory));
        return this;
    }

    private void ReplaceStore(Action<IServiceCollection> register)
    {
        var existing = Services.FirstOrDefault(d => d.ServiceType == typeof(ITaskStateStore));
        if (existing is not null)
            Services.Remove(existing);
        register(Services);
    }
}
