using FreeScheduler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSynchronizerHostService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<RepoSynchronizerHostService> logger,
    Scheduler scheduler) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RepoSynchronizerService>().ScheduleAllTasks();

        // statusService.SyncStatus =
        //     options.Value.SourceRepos
        //         .Select(repo =>
        //             new KeyValuePair<string, SyncStatus>(repo.Key,
        //                 new SyncStatus(null, null, repo.Value, SyncStatusType.Never)))
        //         .ToDictionary();
        //
        // Task.Run(async () =>
        // {
        //     while (!cancellationToken.IsCancellationRequested)
        //     {
        //         logger.LogInformation("Start Sync With Upstream Repo");
        //         foreach (var repo in options.Value.SourceRepos)
        //         {
        //             var repoId = repo.Key;
        //             var repoUrl = repo.Value;
        //
        //             logger.LogInformation("Starting Sync With {RepoId}@{RepoUrl}", repoId, repoUrl);
        //
        //             var syncStartTime = DateTimeOffset.Now;
        //             statusService.SyncStatus[repoId] =
        //                 new SyncStatus(syncStartTime, null, repoUrl, SyncStatusType.Syncing);
        //
        //             try
        //             {
        //                 using var scope = serviceScopeFactory.CreateScope();
        //                 var repoSynchronizerService =
        //                     scope.ServiceProvider.GetRequiredService<RepoSynchronizerService>();
        //
        //                 await repoSynchronizerService.StartSync(repoUrl, repoId);
        //
        //                 logger.LogInformation("Successfully Sync With {RepoUrl}", repo);
        //
        //                 statusService.SyncStatus[repoId] =
        //                     new SyncStatus(syncStartTime, DateTimeOffset.Now, repoUrl, SyncStatusType.Success);
        //             }
        //             catch (Exception e)
        //             {
        //                 logger.LogError(e, "Failed to Sync With {RepoId}@{RepoUrl}", repoId, repoUrl);
        //
        //                 statusService.SyncStatus[repoId] =
        //                     new SyncStatus(syncStartTime, DateTimeOffset.Now, repoUrl, SyncStatusType.Failed, e.ToString());
        //             }
        //         }
        //
        //         logger.LogInformation("Sync With Upstream Repo Complete");
        //
        //         await Task.Delay(TimeSpan.FromSeconds(options.Value.SyncPeriod), cancellationToken);
        //     }
        // }, cancellationToken).ConfigureAwait(false);
        //
        // return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        scheduler.Dispose();
        return Task.CompletedTask;
    }
}