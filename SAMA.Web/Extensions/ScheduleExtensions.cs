using CronExpressionDescriptor;
using Quartz;

namespace SAMA.Web.Extensions;

public static class ScheduleExtensions
{
    private const int DefaultIntervalSeconds = 300;
    private const int MinIntervalSeconds = 10;
    private const int MaxIntervalSeconds = 86400;

    public static bool IsCronExpression(string schedule) => !int.TryParse(schedule, out _);

    public static string? ValidateSchedule(string schedule)
    {
        if (int.TryParse(schedule, out var seconds))
        {
            if (seconds < MinIntervalSeconds)
            {
                return $"Interval must be at least {MinIntervalSeconds} seconds.";
            }

            if (seconds > MaxIntervalSeconds)
            {
                return $"Interval cannot exceed {MaxIntervalSeconds} seconds. Use a cron expression for longer schedules.";
            }

            return null;
        }

        try
        {
            var cron = new CronExpression(schedule);
            cron.GetNextValidTimeAfter(DateTimeOffset.UtcNow);
            return null;
        }
        catch
        {
            return "Invalid schedule. Enter an interval in seconds or a valid cron expression.";
        }
    }

    public static string GetCronDescription(string schedule)
    {
        try
        {
            return ExpressionDescriptor.GetDescription(schedule, new Options
            {
                DayOfWeekStartIndexZero = false
            });
        }
        catch
        {
            return schedule;
        }
    }

    /// <summary>
    /// Returns the expected interval in seconds between the given reference time and the next fire time.
    /// For numeric schedules, returns the value directly.
    /// For cron expressions, computes the next fire time after <paramref name="after"/> to handle uneven schedules correctly.
    /// </summary>
    public static int GetExpectedIntervalSeconds(string schedule, DateTimeOffset after, TimeZoneInfo? timeZone = null)
    {
        if (int.TryParse(schedule, out var seconds))
        {
            return seconds;
        }

        try
        {
            var cron = new CronExpression(schedule);
            if (timeZone != null)
            {
                cron.TimeZone = timeZone;
            }
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
                >= 86400 when seconds % 86400 == 0 => Pluralize(seconds / 86400, "day"),
                >= 3600 when seconds % 3600 == 0 => Pluralize(seconds / 3600, "hour"),
                >= 60 when seconds % 60 == 0 => Pluralize(seconds / 60, "minute"),
                _ => Pluralize(seconds, "second")
            };
        }

        return GetCronDescription(schedule);
    }

    private static string Pluralize(int value, string unit) =>
        value == 1 ? $"Every {unit}" : $"Every {value} {unit}s";
}
