using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace VPMReposSynchronizer.Core.Services.RepoSync;

public class RepoSynchronizerHostService(
    IServiceScopeFactory serviceScopeFactory,
    RepoSyncTaskScheduleService repoSyncTaskScheduleService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var repoSyncTaskService = scope.ServiceProvider.GetRequiredService<RepoSyncTaskService>();

        await repoSyncTaskService.MarkAllUnCompletedTaskAsInterruptedAsync();

        await repoSyncTaskScheduleService.ScheduleAllTasks();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}