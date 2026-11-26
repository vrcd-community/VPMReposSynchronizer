using Cronos;

namespace VPMReposSynchronizer.Core.Utils;

/// <summary>
/// 校验仓库的 cron 表达式是否能被调度库安全等待。
/// FluentScheduler 用 <see cref="Task.Delay(TimeSpan, CancellationToken)"/> 睡到下次执行时间，
/// 单次延时上限约为 49.7 天（uint.MaxValue - 1 毫秒），超过上限会抛 ArgumentOutOfRangeException。
/// </summary>
public static class CronScheduleGuard
{
    // 留出时区偏移的余量：计算使用 UTC，实际等待使用本地时间，最大偏移十余小时。
    public const int MaxNextOccurrenceDays = 49;

    public static bool TryValidate(string? cron, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(cron) || !CronExpression.TryParse(cron, out var expression))
        {
            error = "Invalid cron expression.";
            return false;
        }

        DateTime? nextOccurrence;

        try
        {
            var now = DateTime.UtcNow;
            nextOccurrence = expression.GetNextOccurrence(now, TimeZoneInfo.Utc);

            if (nextOccurrence is null)
            {
                error = "Cron expression never occurs.";
                return false;
            }

            if (nextOccurrence.Value - now > TimeSpan.FromDays(MaxNextOccurrenceDays))
            {
                error = $"Cron next occurrence is more than {MaxNextOccurrenceDays} days away.";
                return false;
            }
        }
        catch (Exception)
        {
            error = "Invalid cron expression.";
            return false;
        }

        return true;
    }
}
