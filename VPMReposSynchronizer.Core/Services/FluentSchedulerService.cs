using System.Collections.ObjectModel;
using FluentScheduler;
using Microsoft.Extensions.Logging;

namespace VPMReposSynchronizer.Core.Services;

public class FluentSchedulerService(ILogger<FluentSchedulerService> logger)
{
    public ReadOnlyDictionary<string, (string, Schedule)> AllSchedules => _allSchedules.AsReadOnly();
    private readonly Dictionary<string, (string, Schedule)> _allSchedules = [];

    public string AddSchedule(string scheduleName, Schedule schedule, string? scheduleKey = null)
    {
        scheduleKey ??= Guid.NewGuid().ToString();

        _allSchedules.Add(scheduleKey, (scheduleName, schedule));

        schedule.Start();

        schedule.JobStarted += (_, args) =>
        {
            logger.LogInformation("Schedule job {ScheduleName} Started At {StartedTime}", scheduleName, args.StartTime);
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

    public async Task RemoveScheduleAsync(string key)
    {
        if (!_allSchedules.TryGetValue(key, out var schedule))
        {
            throw new KeyNotFoundException("Schedule with specify id not exist");
        }

        await Task.Run(schedule.Item2.StopAndBlock);

        _allSchedules.Remove(key);
    }

    public async Task RemoveAllSchedulesAsync()
    {
        var stopTasks = _allSchedules.Select(schedulePair => Task.Run(schedulePair.Value.Item2.StopAndBlock)).ToArray();

        await Task.WhenAll(stopTasks);

        _allSchedules.Clear();
    }
}
