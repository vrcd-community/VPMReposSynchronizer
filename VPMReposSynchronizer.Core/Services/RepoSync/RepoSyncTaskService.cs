using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Entity;

namespace VPMReposSynchronizer.Core.Services.RepoSync;

public partial class RepoSyncTaskService(DefaultDbContext defaultDbContext)
{
    public async ValueTask<long> AddSyncTaskAsync(string repoId, string logPath,
        SyncTaskStatus status = SyncTaskStatus.Pending)
    {
        var syncTaskEntity = new SyncTaskEntity
        {
            RepoId = repoId,
            LogPath = logPath,
            StartTime = DateTimeOffset.Now,
            EndTime = null,
            Status = status
        };

        var entity = defaultDbContext.SyncTasks.Add(syncTaskEntity);
        await defaultDbContext.SaveChangesAsync();

        return entity.Entity.Id;
    }

    public async Task UpdateSyncTaskAsync(long id, string logPath, SyncTaskStatus status = SyncTaskStatus.Running)
    {
        var syncTaskEntity = await defaultDbContext.SyncTasks.FindAsync(id);

        if (syncTaskEntity is null) throw new InvalidOperationException($"Sync task with id {id} not found");

        syncTaskEntity.LogPath = logPath;
        syncTaskEntity.Status = status;

        await UpdateSyncTaskAsync(syncTaskEntity);
    }

    public async Task UpdateSyncTaskAsync(long id, DateTimeOffset endTime, SyncTaskStatus status)
    {
        var syncTaskEntity = await defaultDbContext.SyncTasks.FindAsync(id);

        if (syncTaskEntity is null) throw new InvalidOperationException($"Sync task with id {id} not found");

        syncTaskEntity.EndTime = endTime;
        syncTaskEntity.Status = status;

        await UpdateSyncTaskAsync(syncTaskEntity);
    }

    public async Task UpdateSyncTaskAsync(SyncTaskEntity syncTaskEntity)
    {
        defaultDbContext.SyncTasks.Update(syncTaskEntity);
        await defaultDbContext.SaveChangesAsync();
    }

    public async Task MarkAllUnCompletedTaskAsInterruptedAsync()
    {
        var currentDateTime = DateTimeOffset.Now;

        await defaultDbContext.SyncTasks
            .Where(task => task.Status == SyncTaskStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(task => task.Status, status => SyncTaskStatus.Interrupted)
                .SetProperty(task => task.EndTime, endTime => currentDateTime));
    }

    public async ValueTask<SyncTaskEntity[]> GetSyncTasksAsync(int offset = 0, int limit = 20)
    {
        return await defaultDbContext.SyncTasks
            .OrderByDescending(task => task.Id)
            .Skip(offset)
            .Take(limit)
            .ToArrayAsync();
    }

    public async ValueTask<SyncTaskEntity?> GetSyncTaskAsync(long id)
    {
        return await defaultDbContext.SyncTasks.FindAsync(id);
    }

    public async ValueTask<SyncTaskEntity[]> GetAllLatestSyncTasksAsync()
    {
        return await defaultDbContext.SyncTasks
            .GroupBy(task => task.RepoId)
            .Select(tasks => tasks.OrderByDescending(task => task.Id).First())
            .ToArrayAsync();
    }

    public async ValueTask<SyncTaskEntity?> GetLatestSyncTaskAsync(string repoId)
    {
        return await defaultDbContext.SyncTasks
            .Where(task => task.RepoId == repoId)
            .OrderByDescending(task => task.Id)
            .FirstOrDefaultAsync();
    }

    public static void CleanupExpiredLogFiles()
    {
        var expiredDate = DateTimeOffset.Now - TimeSpan.FromDays(30);

        var logToClear = new List<string>();

        if (!Directory.Exists(RepoSynchronizerService.SyncTaskLoggerPath))
            return;

        var subDirectories = Directory.GetDirectories(RepoSynchronizerService.SyncTaskLoggerPath);

        var logFileNameRegex = LogFileNameRegex();

        foreach (var subDirectory in subDirectories)
        {
            var files = Directory.GetFiles(subDirectory)
                .Where(file => logFileNameRegex.IsMatch(file))
                .Where(file =>
                    logFileNameRegex.Match(file).Groups[1].Value is { } dateStr &&
                    DateTimeOffset.TryParseExact(dateStr, "yyyy-MM-dd-HH-mm-ss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var date)
                    && date < expiredDate);

            logToClear.AddRange(files);
        }

        foreach (var logPathToClear in logToClear)
        {
            File.Delete(logPathToClear);
        }
    }

    [GeneratedRegex(@"\\[\w\d]+-[\w\d]+-(\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}).log")]
    private static partial Regex LogFileNameRegex();
}
