using Microsoft.EntityFrameworkCore;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Entity;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSyncTaskService(DefaultDbContext defaultDbContext)
{
    public async ValueTask<long> AddSyncTaskAsync(string repoId, string logPath, DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null, SyncTaskStatus status = SyncTaskStatus.Running)
    {
        var syncTaskEntity = new SyncTaskEntity
        {
            RepoId = repoId,
            LogPath = logPath,
            StartTime = startTime ?? DateTimeOffset.Now,
            EndTime = endTime,
            Status = status
        };

        var entity = defaultDbContext.SyncTasks.Add(syncTaskEntity);
        await defaultDbContext.SaveChangesAsync();

        return entity.Entity.Id;
    }

    public async Task UpdateSyncTaskAsync(long id, string logPath)
    {
        var syncTaskEntity = await defaultDbContext.SyncTasks.FindAsync(id);

        if (syncTaskEntity is null)
        {
            throw new InvalidOperationException($"Sync task with id {id} not found");
        }

        syncTaskEntity.LogPath = logPath;

        await UpdateSyncTaskAsync(syncTaskEntity);
    }

    public async Task UpdateSyncTaskAsync(long id, DateTimeOffset endTime, SyncTaskStatus status)
    {
        var syncTaskEntity = await defaultDbContext.SyncTasks.FindAsync(id);

        if (syncTaskEntity is null)
        {
            throw new InvalidOperationException($"Sync task with id {id} not found");
        }

        syncTaskEntity.EndTime = endTime;
        syncTaskEntity.Status = status;

        await UpdateSyncTaskAsync(syncTaskEntity);
    }

    public async Task UpdateSyncTaskAsync(SyncTaskEntity syncTaskEntity)
    {
        defaultDbContext.SyncTasks.Update(syncTaskEntity);
        await defaultDbContext.SaveChangesAsync();
    }

    public async ValueTask<SyncTaskEntity[]> GetSyncTasksAsync()
    {
        return await defaultDbContext.SyncTasks.ToArrayAsync();
    }

    public async ValueTask<SyncTaskEntity?> GetSyncTaskAsync(long id)
    {
        return await defaultDbContext.SyncTasks.FindAsync(id);
    }
}