using FluentScheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Utils;

namespace VPMReposSynchronizer.Core.Services.RepoSync;

public class RepoSyncTaskScheduleService(
    IServiceScopeFactory serviceScopeFactory,
    FluentSchedulerService fluentSchedulerService,
    RepoSyncTaskDispatchService repoSyncTaskDispatchService,
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

        var repos = await repoMetaDataService.GetRepos();

        foreach (var repo in repos)
        {
            if (!CronScheduleGuard.TryValidate(repo.SyncTaskCron, out var cronError))
            {
                logger.LogError("Skip scheduling repo {RepoId} with cron {Cron}: {Reason}", repo.Id,
                    repo.SyncTaskCron, cronError);
                continue;
            }

            logger.LogInformation("Adding task for repo {RepoId}", repo.Id);

            try
            {
                var schedule = new Schedule(async () =>
                {
                    await repoSyncTaskDispatchService.StartNewSyncTask(repo.Id);
                }, repo.SyncTaskCron);

                fluentSchedulerService.AddSchedule($"Sync Repos {repo.Id} ({repo.SyncTaskCron}): {repo.UpStreamUrl}",
                    schedule, $"repo-sync-{repo.Id}");
            }
            catch (Exception ex)
            {
                // 单个仓库的调度失败不能阻止其他仓库继续调度
                logger.LogError(ex, "Failed to schedule sync task for repo {RepoId}", repo.Id);
            }
        }

        var clearExpiredLogSchedule =
            new Schedule(RepoSyncTaskService.CleanupExpiredLogFiles, s => s.Every(TimeSpan.FromHours(1)));

        fluentSchedulerService.AddSchedule("Clean Expired Logs", clearExpiredLogSchedule);

        logger.LogInformation("All tasks scheduled");
    }

    public async Task InvokeSyncTaskAsync(string repoId)
    {
        if (!fluentSchedulerService.AllSchedules.TryGetValue($"repo-sync-{repoId}", out var schedulePair))
            throw new KeyNotFoundException("Schedule for the specify repo not found");

        var scope = serviceScopeFactory.CreateAsyncScope();
        var serviceProvider = scope.ServiceProvider;

        var repoSyncTaskService = serviceProvider.GetRequiredService<RepoSyncTaskService>();
        var latestSyncTask = await repoSyncTaskService.GetLatestSyncTaskAsync(repoId);

        if (latestSyncTask?.Status is SyncTaskStatus.Running or SyncTaskStatus.Pending)
            throw new InvalidOperationException("There is already a sync task is running for this repo");

        schedulePair.Item2.Stop();

        _ = Task.Run(async () =>
        {
            logger.LogInformation("Create sync task for repo {RepoId}", repoId);

            try
            {
                await repoSyncTaskDispatchService.StartNewSyncTask(repoId);
            }
            finally
            {
                schedulePair.Item2.Start();
                await scope.DisposeAsync();
            }
        });
    }
}
