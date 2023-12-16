using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VPMReposSynchronizer.Core.Options;

namespace VPMReposSynchronizer.Core.Services;

public class RepoSynchronizerHostService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<SynchronizerOptions> options,
    ILogger<RepoSynchronizerHostService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Start Sync With Upstream Repo");
                foreach (var repoUrl in options.Value.SourceRepoUrls)
                {
                    logger.LogInformation("Starting Sync With {RepoUrl}", repoUrl);
                    try
                    {
                        using var scope = serviceScopeFactory.CreateScope();
                        var repoSynchronizerService =
                            scope.ServiceProvider.GetRequiredService<RepoSynchronizerService>();

                        await repoSynchronizerService.StartSync(repoUrl);

                        logger.LogInformation("Successfully Sync With {RepoUrl}", repoUrl);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failed to Sync With {RepoUrl}", repoUrl);
                    }
                }

                logger.LogInformation("Sync With Upstream Repo Complete");

                await Task.Delay(TimeSpan.FromSeconds(options.Value.SyncPeriod), cancellationToken);
            }
        }, cancellationToken).ConfigureAwait(false);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}