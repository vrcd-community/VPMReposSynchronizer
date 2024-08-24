using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.Options;

namespace VPMReposSynchronizer.Core.Services.RepoSync;

public class RepoSyncTaskDispatchService(IServiceScopeFactory serviceScopeFactory, IOptions<SyncOptions> options)
{
    private readonly List<Task> _currentTasks = [];

    public async Task StartNewSyncTask(string repoId)
    {
        using var scope = serviceScopeFactory.CreateScope();

        var repoSyncTaskService = scope.ServiceProvider.GetRequiredService<RepoSyncTaskService>();

        var taskId = await repoSyncTaskService.AddSyncTaskAsync(repoId, "");

        using var cancellationTokenSource = new CancellationTokenSource();
        var task = Task.Run(async () => await Task.Delay(Timeout.Infinite), cancellationTokenSource.Token);

        _currentTasks.Add(task);

        if (_currentTasks.Count > options.Value.MaxConcurrentTasks)
        {
            var lastTask = _currentTasks[^1];
            try
            {
                lastTask.GetAwaiter().GetResult();
            }
            catch (TaskCanceledException)
            {
                // ignore
            }
        }

        var repoSynchronizerService = scope.ServiceProvider.GetRequiredService<RepoSynchronizerService>();

        try
        {
            await repoSynchronizerService.StartSync(taskId);
        }
        finally
        {
            await cancellationTokenSource.CancelAsync();

            _currentTasks.Remove(task);
        }
    }
}
