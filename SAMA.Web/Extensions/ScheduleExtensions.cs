using Quartz;

namespace SAMA.Web.Extensions;

public static class ScheduleExtensions
{
    private const int DefaultIntervalSeconds = 300;

    /// <summary>
    /// Returns the expected interval in seconds between the given reference time and the next fire time.
    /// For numeric schedules, returns the value directly.
    /// For cron expressions, computes the next fire time after <paramref name="after"/> to handle uneven schedules correctly.
    /// </summary>
    public static int GetExpectedIntervalSeconds(string schedule, DateTimeOffset after)
    {
        if (int.TryParse(schedule, out var seconds))
        {
            return seconds;
        }

        try
        {
            var cron = new CronExpression(schedule);
            var nextFire = cron.GetNextValidTimeAfter(after);
            if (nextFire.HasValue)
            {
                return Math.Max(1, (int)(nextFire.Value - after).TotalSeconds);
            }
        }
        catch
        {
            // Invalid cron expression
        }

        return DefaultIntervalSeconds;
    }

    public static string ToDisplayString(string schedule)
    {
        if (int.TryParse(schedule, out var seconds))
        {
            return seconds switch
            {
                >= 86400 when seconds % 86400 == 0 => $"Every {seconds / 86400}d",
                >= 3600 when seconds % 3600 == 0 => $"Every {seconds / 3600}h",
                >= 60 when seconds % 60 == 0 => $"Every {seconds / 60}m",
                _ => $"Every {seconds}s"
            };
        }

        return schedule;
    }

    public static string ToDetailedDisplayString(string schedule)
    {
        if (int.TryParse(schedule, out var seconds))
        {
            return seconds switch
            {
                >= 86400 when seconds % 86400 == 0 => $"Every {seconds / 86400} day(s)",
                >= 3600 when seconds % 3600 == 0 => $"Every {seconds / 3600} hour(s)",
                >= 60 when seconds % 60 == 0 => $"Every {seconds / 60} minute(s)",
                _ => $"Every {seconds} second(s)"
            };
        }

        return $"Cron: {schedule}";
    }
}
