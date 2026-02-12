namespace SAMA.Web.Extensions;

public static class TimeZoneExtensions
{
    public static List<(string IanaId, string DisplayName)> GetIanaTimeZones()
    {
        return TimeZoneInfo.GetSystemTimeZones()
            .Select(tz =>
            {
                if (tz.HasIanaId)
                {
                    return (tz.Id, tz.DisplayName);
                }

                return TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var ianaId)
                    ? (ianaId, tz.DisplayName)
                    : (tz.Id, tz.DisplayName);
            })
            .DistinctBy(x => x.Item1)
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    public static TimeZoneInfo FindTimeZoneByIanaId(string ianaId)
    {
        return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
    }
}
