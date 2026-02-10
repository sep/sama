namespace SAMA.Web.Extensions;

public static class ScheduleExtensions
{
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
