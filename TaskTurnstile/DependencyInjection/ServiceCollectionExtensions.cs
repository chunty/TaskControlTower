using TaskTurnstile.Stores;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace TaskTurnstile.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static TaskTurnstileBuilder AddTaskTurnstile(
        this IServiceCollection services,
        Action<TaskTurnstileOptions>? configure = null)
    {
        var options = new TaskTurnstileOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Default: private MemoryDistributedCache instance — isolated from the app's own cache.
        // Call .AddDistributedStore() on the builder to use the app's registered IDistributedCache instead.
        services.AddSingleton<ITaskStateStore>(_ =>
            new DistributedCacheTaskStateStore(
                new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
                options.KeyPrefix));

        services.AddSingleton<ITaskStateManager, TaskStateManager>();

        if (options.CleanupOnStartup)
            services.AddSingleton<IHostedService, CleanupOnStartupHostedService>();

        return new TaskTurnstileBuilder(services);
    }
}
