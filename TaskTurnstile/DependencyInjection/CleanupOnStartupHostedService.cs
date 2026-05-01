using Microsoft.Extensions.Hosting;

namespace TaskTurnstile.DependencyInjection;

internal sealed class CleanupOnStartupHostedService(ITaskStateStore store) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        store.CleanupAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
