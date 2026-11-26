using System.Collections.ObjectModel;
using FluentScheduler;
using Microsoft.Extensions.Logging;

namespace VPMReposSynchronizer.Core.Services;

public class FluentSchedulerService(ILogger<FluentSchedulerService> logger)
{
    private readonly Dictionary<string, (string, Schedule)> _allSchedules = [];
    public ReadOnlyDictionary<string, (string, Schedule)> AllSchedules => _allSchedules.AsReadOnly();

    public string AddSchedule(string scheduleName, Schedule schedule, string? scheduleKey = null)
    {
        scheduleKey ??= Guid.NewGuid().ToString();

        _allSchedules.Add(scheduleKey, (scheduleName, schedule));

        schedule.Start();

        schedule.JobStarted += (_, args) =>
        {
            logger.LogInformation("Schedule job {ScheduleName} Started At {StartedTime}", scheduleName,
                args.StartTime);
        };

        schedule.JobEnded += (_, args) =>
        {
            if (args.Exception is { } exception)
            {
                logger.LogError(exception,
                    "Schedule job {ScheduleName} started at {StartedTime} and ended at {EndTime} Failed, Next Run: {NextRun}",
                    scheduleName, args.StartTime, args.EndTime, args.NextRun);
                return;
            }

            logger.LogInformation(
                "Schedule job {ScheduleName} started at {StartedTime} finished at {FinishedTime}, Next Run: {NextRun}",
                scheduleName, args.StartTime, args.EndTime, args.NextRun);
        };

        return scheduleKey;
    }

    public Task RemoveScheduleAsync(string key)
    {
        if (!_allSchedules.TryGetValue(key, out var schedule))
            throw new KeyNotFoundException("Schedule with specify id not exist");

        _allSchedules.Remove(key);

        // 只请求取消，不等待正在运行的作业：StopAndBlock 会把等待任务的异常传播到调用方，
        // 已经开始的作业让它自行结束即可。
        try
        {
            schedule.Item2.Stop();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop schedule {ScheduleName} ({ScheduleKey})", schedule.Item1, key);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAllSchedulesAsync()
    {
        // 只请求取消，不等待正在运行的作业：StopAndBlock 会把等待任务的异常传播到调用方，
        // 已经开始的作业让它自行结束即可。单个计划停止失败也不能影响其他计划。
        foreach (var (key, (scheduleName, schedule)) in _allSchedules)
        {
            try
            {
                schedule.Stop();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to stop schedule {ScheduleName} ({ScheduleKey})", scheduleName, key);
            }
        }

        _allSchedules.Clear();

        return Task.CompletedTask;
    }
}
