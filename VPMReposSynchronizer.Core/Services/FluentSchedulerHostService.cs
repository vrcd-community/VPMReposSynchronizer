using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VPMReposSynchronizer.Core.Services;

public class FluentSchedulerHostService(
    ILogger<FluentSchedulerHostService> logger,
    FluentSchedulerService fluentSchedulerService) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Waiting for all the jobs to finish");

        await fluentSchedulerService.RemoveAllSchedulesAsync();

        logger.LogInformation("All jobs Stopped");
    }
}