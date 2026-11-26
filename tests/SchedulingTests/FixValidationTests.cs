using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using VPMReposSynchronizer.Core.Models.Mappers;
using VPMReposSynchronizer.Core.Models.Types.RepoAdmin;
using VPMReposSynchronizer.Core.Services;
using VPMReposSynchronizer.Core.Utils;
using VPMReposSynchronizer.Entry.Controllers;
using Xunit;

namespace SchedulingTests;

/// <summary>
/// 补丁后的行为：cron 上限校验、单仓失败隔离、坏计划不进入计划表。
/// </summary>
public class FixValidationTests
{
    private static string CronForDaysAhead(int days)
    {
        var target = DateTime.UtcNow.AddDays(days);
        return $"0 0 {target.Day} {target.Month} *";
    }

    private static IMapper CreateMapper() =>
        new MapperConfiguration(configuration => configuration.AddProfile<RepoAdminProfile>()).CreateMapper();

    [Fact]
    public void Guard_AcceptsNearCron_RejectsFarMalformedAndNeverOccurring()
    {
        Assert.True(CronScheduleGuard.TryValidate("*/10 * * * *", out _));

        var near = CronForDaysAhead(10);
        Assert.True(CronScheduleGuard.TryValidate(near, out var nearError));
        Assert.Equal(string.Empty, nearError);

        var far = CronForDaysAhead(60);
        Assert.False(CronScheduleGuard.TryValidate(far, out var farError));
        Assert.Contains("49", farError);

        Assert.False(CronScheduleGuard.TryValidate("not a cron", out _));

        Assert.False(CronScheduleGuard.TryValidate("0 0 31 2 *", out var neverError));
        Assert.Contains("never", neverError);
    }

    [Fact]
    public void Guard_Boundary_AcceptsInsideLimit_RejectsOutsideLimit()
    {
        var inside = CronForDaysAhead(45);
        var outside = CronForDaysAhead(51);

        Assert.True(CronScheduleGuard.TryValidate(inside, out var insideError));
        Assert.Equal(string.Empty, insideError);
        Assert.False(CronScheduleGuard.TryValidate(outside, out var outsideError));
        Assert.Contains("49", outsideError);
    }

    [Fact]
    public async Task Controller_Create_RejectsFarFutureCron()
    {
        var controller = new RepoController(null!, null!, CreateMapper());

        var result = await controller.Create(new RepoAdminUpdateDto
        {
            ApiId = "repo-x",
            UpstreamUrl = "https://example.invalid/repo-x",
            Description = "test",
            SyncTaskCron = CronForDaysAhead(60)
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("49", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task Controller_Update_RejectsFarFutureCron()
    {
        await using var harness = await SchedulerHarness.CreateAsync();
        await harness.SeedRepoAsync("repo-a", "*/10 * * * *");

        await using var scope = harness.Provider.CreateAsyncScope();
        var repoMetaDataService = scope.ServiceProvider.GetRequiredService<RepoMetaDataService>();
        var controller = new RepoController(null!, repoMetaDataService, CreateMapper());

        var result = await controller.Update("repo-a", new RepoAdminUpdateDto
        {
            ApiId = "repo-a",
            UpstreamUrl = "https://example.invalid/repo-a",
            Description = "test",
            SyncTaskCron = CronForDaysAhead(60)
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("49", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task Rebuild_SkipsFarFutureRepo_AndKeepsOthersScheduled()
    {
        await using var harness = await SchedulerHarness.CreateAsync();
        await harness.SeedRepoAsync("repo-a", "*/10 * * * *");
        await harness.SeedRepoAsync("repo-bad", CronForDaysAhead(60));
        await harness.SeedRepoAsync("repo-c", "*/10 * * * *");

        await harness.Scheduling.ScheduleAllTasks();
        await harness.Scheduling.ScheduleAllTasks();

        Assert.True(harness.ScheduleFor("repo-a")?.Running);
        Assert.True(harness.ScheduleFor("repo-c")?.Running);
        Assert.Null(harness.ScheduleFor("repo-bad"));
        Assert.Contains(harness.Fluent.AllSchedules.Values, pair => pair.Item1 == "Clean Expired Logs");
    }
}
