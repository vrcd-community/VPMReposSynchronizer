using FluentScheduler;
using Xunit;

namespace SchedulingTests;

/// <summary>
/// 基线（4733efe）上的反例：这些断言在当前 main 上应当失败。
/// </summary>
public class BaselineTests
{
    [Fact]
    public void FarFutureCron_StopAndBlock_ThrowsTimerLimit()
    {
        var schedule = new Schedule(() => { }, CronTestData.FarFutureCron());

        schedule.Start();

        var exception = Assert.Throws<AggregateException>(() => schedule.StopAndBlock());
        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    [Fact]
    public async Task Rebuild_DoesNotBlockOnRunningJob()
    {
        await using var harness = await SchedulerHarness.CreateAsync();
        await harness.SeedRepoAsync("repo-a", "*/10 * * * *");

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var blocking = new Schedule(async () =>
            {
                started.TrySetResult(true);
                await release.Task;
            }, run => run.Now());

            harness.Fluent.AddSchedule("blocking-job", blocking);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var rebuild = harness.Scheduling.ScheduleAllTasks();
            var winner = await Task.WhenAny(rebuild, Task.Delay(TimeSpan.FromSeconds(3)));

            release.TrySetResult(true);

            try
            {
                await rebuild.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch
            {
                // 基线可能在重建结束后抛异常，不影响“是否阻塞”的判定
            }

            Assert.Same(rebuild, winner);
        }
        finally
        {
            release.TrySetResult(true);
        }
    }

    [Fact]
    public async Task Rebuild_WithFarFutureRepo_KeepsOtherReposScheduled()
    {
        await using var harness = await SchedulerHarness.CreateAsync();
        await harness.SeedRepoAsync("repo-a", "*/10 * * * *");
        await harness.SeedRepoAsync("repo-bad", CronTestData.FarFutureCron());
        await harness.SeedRepoAsync("repo-c", "*/10 * * * *");

        await harness.Scheduling.ScheduleAllTasks();

        var exception = await Record.ExceptionAsync(() => harness.Scheduling.ScheduleAllTasks());

        Assert.Null(exception);
        Assert.True(harness.ScheduleFor("repo-a")?.Running);
        Assert.True(harness.ScheduleFor("repo-c")?.Running);
    }

    [Fact]
    public async Task Rebuild_WithMalformedRepo_SchedulesRemainingRepos()
    {
        await using var harness = await SchedulerHarness.CreateAsync();
        await harness.SeedRepoAsync("repo-a", "*/10 * * * *");
        await harness.SeedRepoAsync("repo-bad", "not a cron");
        await harness.SeedRepoAsync("repo-c", "*/10 * * * *");

        var exception = await Record.ExceptionAsync(() => harness.Scheduling.ScheduleAllTasks());

        Assert.Null(exception);
        Assert.True(harness.ScheduleFor("repo-a")?.Running);
        Assert.True(harness.ScheduleFor("repo-c")?.Running);
    }
}
