using FluentScheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VPMReposSynchronizer.Core.Models.Entity;

namespace VPMReposSynchronizer.Core.Services.RepoSync;

public class RepoSyncTaskScheduleService(
    IServiceScopeFactory serviceScopeFactory,
    FluentSchedulerService fluentSchedulerService,
    ILogger<RepoSyncTaskScheduleService> logger)
{
    public async Task ScheduleAllTasks()
    {
        logger.LogInformation("Removing all exists tasks");
        await fluentSchedulerService.RemoveAllSchedulesAsync();

        await ScheduleAllTasksInternal();
    }

    private async Task ScheduleAllTasksInternal()
    {
        logger.LogInformation("Scheduling all tasks");

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var repoMetaDataService = scope.ServiceProvider.GetRequiredService<RepoMetaDataService>();

        var repos = await repoMetaDataService.GetAllRepos();

        foreach (var repo in repos)
        {
            logger.LogInformation("Adding task for repo {RepoId}", repo.Id);

            var schedule = new Schedule(async () =>
            {
                using var jobScope = serviceScopeFactory.CreateScope();
                var repoSynchronizerService = jobScope.ServiceProvider.GetRequiredService<RepoSynchronizerService>();

                await repoSynchronizerService.StartSync(repo.Id);
            }, repo.SyncTaskCron);

            fluentSchedulerService.AddSchedule($"Sync Repos {repo.Id} ({repo.SyncTaskCron}): {repo.UpStreamUrl}",
                schedule, $"repo-sync-{repo.Id}");
        }

        logger.LogInformation("All tasks scheduled");
    }

    public async Task InvokeSyncTaskAsync(string repoId)
    {
        if (!fluentSchedulerService.AllSchedules.TryGetValue($"repo-sync-{repoId}", out var schedulePair))
        {
            throw new KeyNotFoundException("Schedule for the specify repo not found");
        }

        var scope = serviceScopeFactory.CreateAsyncScope();
        var serviceProvider = scope.ServiceProvider;

        var repoSyncTaskService = serviceProvider.GetRequiredService<RepoSyncTaskService>();
        var latestSyncTask = await repoSyncTaskService.GetLatestSyncTaskAsync(repoId);

        if (latestSyncTask is not null && latestSyncTask.Status == SyncTaskStatus.Running)
        {
            throw new InvalidOperationException("There is already a sync task is running for this repo");
        }

        var repoSynchronizerService = serviceProvider.GetRequiredService<RepoSynchronizerService>();

        schedulePair.Item2.Stop();

        _ = Task.Run(async () =>
        {
            logger.LogInformation("Create sync task for repo {RepoId}", repoId);

            try
            {
                await repoSynchronizerService.StartSync(repoId);
            }
            finally
            {
                schedulePair.Item2.Start();
                await scope.DisposeAsync();
            }
        });
    }
}
