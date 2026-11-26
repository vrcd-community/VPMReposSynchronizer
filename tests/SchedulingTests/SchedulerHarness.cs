using FluentScheduler;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VPMReposSynchronizer.Core.DbContexts;
using VPMReposSynchronizer.Core.Models.Entity;
using VPMReposSynchronizer.Core.Models.Mappers;
using VPMReposSynchronizer.Core.Options;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Services.RepoSync;

namespace SchedulingTests;

/// <summary>
/// 只使用 :memory: SQLite 和本地计时器；所有 cron 都指向未来，不会启动真实同步任务。
/// </summary>
internal sealed class SchedulerHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public ServiceProvider Provider { get; }
    public FluentSchedulerService Fluent { get; }
    public RepoSyncTaskScheduleService Scheduling { get; }

    private SchedulerHarness(ServiceProvider provider, SqliteConnection connection)
    {
        Provider = provider;
        _connection = connection;
        Fluent = provider.GetRequiredService<FluentSchedulerService>();
        Scheduling = provider.GetRequiredService<RepoSyncTaskScheduleService>();
    }

    public static async Task<SchedulerHarness> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();

        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<DefaultDbContext>(options => options.UseSqlite(connection));
        services.AddAutoMapper(typeof(VpmPackageProfile));

        services.Configure<SyncOptions>(_ => { });
        services.AddTransient<RepoMetaDataService>();
        services.AddTransient<RepoSyncTaskService>();
        services.AddSingleton<RepoSyncTaskDispatchService>();
        services.AddSingleton<RepoSyncTaskScheduleService>();
        services.AddSingleton<FluentSchedulerService>();

        var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DefaultDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new SchedulerHarness(provider, connection);
    }

    public async Task SeedRepoAsync(string id, string cron)
    {
        await using var scope = Provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DefaultDbContext>();

        db.Repos.Add(new VpmRepoEntity
        {
            Id = id,
            Description = "scheduling verification fixture",
            UpStreamUrl = $"https://example.invalid/{id}",
            SyncTaskCron = cron
        });

        await db.SaveChangesAsync();
    }

    public Schedule? ScheduleFor(string repoId) =>
        Fluent.AllSchedules.TryGetValue($"repo-sync-{repoId}", out var pair) ? pair.Item2 : null;

    public async ValueTask DisposeAsync()
    {
        // 不阻塞地停掉所有测试计划，避免未结束的等待任务泄露到其他测试
        foreach (var pair in Fluent.AllSchedules.Values.ToArray())
        {
            try
            {
                pair.Item2.Stop();
            }
            catch
            {
                // 测试清理忽略
            }
        }

        await Provider.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal static class CronTestData
{
    /// <summary>
    /// 生成 60 天以后才执行一次的 cron，用于触发调度库 Task.Delay 的计时器上限。
    /// </summary>
    public static string FarFutureCron()
    {
        var target = DateTime.UtcNow.AddDays(60);
        return $"0 0 {target.Day} {target.Month} *";
    }
}
