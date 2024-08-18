using VPMReposSynchronizer.Core.Models.Types;
using VPMReposSynchronizer.Core.Services.RepoSync;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSyncStatusService(RepoMetaDataService repoMetaDataService, RepoSyncTaskService repoSyncTaskService)
{
    public async Task<SyncStatusPublic[]> GetAllSyncStatusAsync()
    {
        var latestSyncTasks = await repoSyncTaskService.GetAllLatestSyncTasksAsync();

        var repoUpstreamUrls = (await repoMetaDataService.GetAllRepos())
            .ToDictionary(repo => repo.Id, repo => repo.UpStreamUrl);

        return latestSyncTasks
            .Select(task => new SyncStatusPublic(
                SyncEnded: task.EndTime,
                SyncStarted: task.StartTime,
                Status: task.Status,
                SyncTaskId: task.Id,
                RepoId: task.RepoId,
                RepoUpstreamUrl: repoUpstreamUrls[task.RepoId],
                Message: ""
            ))
            .ToArray();
    }

    public async Task<SyncStatusPublic?> GetSyncStatusAsync(string repoId)
    {
        var repo = await repoMetaDataService.GetRepoById(repoId);
        if (repo is null)
            return null;

        var latestSyncTask = await repoSyncTaskService.GetLatestSyncTaskAsync(repoId);
        if (latestSyncTask is null) return null;

        return new SyncStatusPublic(
            SyncEnded: latestSyncTask.EndTime,
            SyncStarted: latestSyncTask.StartTime,
            Status: latestSyncTask.Status,
            SyncTaskId: latestSyncTask.Id,
            RepoId: latestSyncTask.RepoId,
            RepoUpstreamUrl: repo.UpStreamUrl,
            Message: ""
        );
    }
}