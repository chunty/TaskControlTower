using TaskTurnstile.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace TaskTurnstile.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    // ── Registration ──────────────────────────────────────────────────────────

    [Fact]
    public void AddTaskTurnstile_RegistersITaskStateManager()
    {
        var sp = new ServiceCollection().AddTaskTurnstile().Services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ITaskStateManager>());
    }

    [Fact]
    public void AddTaskTurnstile_RegistersITaskStateStore()
    {
        var sp = new ServiceCollection().AddTaskTurnstile().Services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<ITaskStateStore>());
    }

    // ── CleanupOnStartup ──────────────────────────────────────────────────────

    [Fact]
    public void AddTaskTurnstile_CleanupOnStartup_True_RegistersHostedService()
    {
        var sp = new ServiceCollection()
            .AddTaskTurnstile(o => o.CleanupOnStartup = true)
            .Services.BuildServiceProvider();

        Assert.Contains(sp.GetServices<IHostedService>(), s => s is CleanupOnStartupHostedService);
    }

    [Fact]
    public void AddTaskTurnstile_CleanupOnStartup_False_DoesNotRegisterHostedService()
    {
        var sp = new ServiceCollection()
            .AddTaskTurnstile(o => o.CleanupOnStartup = false)
            .Services.BuildServiceProvider();

        Assert.DoesNotContain(sp.GetServices<IHostedService>(), s => s is CleanupOnStartupHostedService);
    }

    [Fact]
    public async Task CleanupOnStartupHostedService_CallsStoreCleanupAsync_OnStart()
    {
        var store = Substitute.For<ITaskStateStore>();
        var svc = new CleanupOnStartupHostedService(store);

        await svc.StartAsync(CancellationToken.None);

        await store.Received(1).CleanupAsync(Arg.Any<CancellationToken>());
    }

    // ── AddDistributedStore ───────────────────────────────────────────────────

    [Fact]
    public async Task AddDistributedStore_UsesRegisteredDistributedCache()
    {
        var appCache = Substitute.For<IDistributedCache>();
        appCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<byte[]?>(null));

        var sp = new ServiceCollection()
            .AddSingleton<IDistributedCache>(appCache)
            .AddTaskTurnstile().AddDistributedStore()
            .Services.BuildServiceProvider();

        var store = sp.GetRequiredService<ITaskStateStore>();
        await store.IsRunningAsync("job");

        await appCache.Received(1).GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── UseTaskStateStore ─────────────────────────────────────────────────────

    [Fact]
    public void UseTaskStateStore_ReplacesDefaultStore()
    {
        var customStore = Substitute.For<ITaskStateStore>();

        var sp = new ServiceCollection()
            .AddTaskTurnstile().UseTaskStateStore(_ => customStore)
            .Services.BuildServiceProvider();

        Assert.Same(customStore, sp.GetRequiredService<ITaskStateStore>());
    }

    [Fact]
    public void UseTaskStateStore_CalledTwice_LastRegistrationWins()
    {
        var first = Substitute.For<ITaskStateStore>();
        var second = Substitute.For<ITaskStateStore>();

        var sp = new ServiceCollection()
            .AddTaskTurnstile()
            .UseTaskStateStore(_ => first)
            .UseTaskStateStore(_ => second)
            .Services.BuildServiceProvider();

        Assert.Same(second, sp.GetRequiredService<ITaskStateStore>());
    }
}
