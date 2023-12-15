using Microsoft.Extensions.Hosting;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSynchronizerHostService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}