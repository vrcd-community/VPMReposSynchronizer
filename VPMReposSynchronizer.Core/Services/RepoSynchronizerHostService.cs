using FreeScheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSynchronizerHostService(
    RepoSyncTaskScheduleService repoSyncTaskScheduleService,
    Scheduler scheduler) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await repoSyncTaskScheduleService.ScheduleAllTasks();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        scheduler.Dispose();
        return Task.CompletedTask;
    }
}