using Microsoft.Extensions.Hosting;

namespace VPMReposSynchronizer.Core.Services.RepoSync;

public class RepoSynchronizerHostService(
    RepoSyncTaskScheduleService repoSyncTaskScheduleService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await repoSyncTaskScheduleService.ScheduleAllTasks();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
