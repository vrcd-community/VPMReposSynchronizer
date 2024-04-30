using FreeScheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace VPMReposSynchronizer.Core.Services.RepoSync;

public class RepoSyncTaskScheduleService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RepoSyncTaskScheduleService> logger,
    Scheduler scheduler)
{
    private readonly List<string> _taskIds = [];

    public async Task ScheduleAllTasks()
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var repoMetaDataService = scope.ServiceProvider.GetRequiredService<RepoMetaDataService>();

        logger.LogInformation("Scheduling all tasks");
        var repos = await repoMetaDataService.GetAllRepos();

        logger.LogInformation("Removing all exists tasks");
        foreach (var taskId in _taskIds)
        {
            logger.LogInformation("Removing task {TaskId}", taskId);
            scheduler.RemoveTask(taskId);
        }

        logger.LogInformation("Adding new tasks");
        foreach (var repo in repos)
        {
            logger.LogInformation("Adding task for repo {RepoId}", repo.Id);
            var taskId = scheduler.AddTaskCustom("SyncRepo", repo.Id, repo.SyncTaskCron);
            _taskIds.Add(taskId);
        }

        logger.LogInformation("All tasks scheduled");
    }

    public async void InvokeSyncTask(string repoId)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var repoSynchronizerService = scope.ServiceProvider.GetRequiredService<RepoSynchronizerService>();

        logger.LogInformation("Invoking sync task for repo {RepoId}", repoId);
        await repoSynchronizerService.StartSync(repoId);
        logger.LogInformation("Sync task for repo {RepoId} invoked", repoId);
    }
}