using SAMA.Web.Extensions;

namespace SAMA.Tests.Unit.Web.Extensions;

[TestClass]
public class ScheduleExtensionsTests
{
    private static readonly DateTimeOffset ReferenceTime = new(2026, 2, 10, 12, 0, 0, TimeSpan.Zero); // Tuesday noon UTC

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldReturnSecondsForNumericSchedule()
    {
        var result = ScheduleExtensions.GetExpectedIntervalSeconds("60", ReferenceTime);

        Assert.AreEqual(60, result);
    }

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldReturnLargeValueForNumericSchedule()
    {
        var result = ScheduleExtensions.GetExpectedIntervalSeconds("3600", ReferenceTime);

        Assert.AreEqual(3600, result);
    }

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldReturnDerivedIntervalForCronEveryFiveMinutes()
    {
        var result = ScheduleExtensions.GetExpectedIntervalSeconds("0 */5 * * * ?", ReferenceTime);

        Assert.AreEqual(300, result);
    }

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldReturnDerivedIntervalForCronEveryHour()
    {
        var result = ScheduleExtensions.GetExpectedIntervalSeconds("0 0 * * * ?", ReferenceTime);

        Assert.AreEqual(3600, result);
    }

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldReturnDerivedIntervalForCronEveryMinute()
    {
        var result = ScheduleExtensions.GetExpectedIntervalSeconds("0 * * * * ?", ReferenceTime);

        Assert.AreEqual(60, result);
    }

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldReturnDefaultForInvalidCron()
    {
        var result = ScheduleExtensions.GetExpectedIntervalSeconds("not-valid", ReferenceTime);

        Assert.AreEqual(300, result);
    }

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldReturnDefaultForEmptyString()
    {
        var result = ScheduleExtensions.GetExpectedIntervalSeconds(string.Empty, ReferenceTime);

        Assert.AreEqual(300, result);
    }

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldReturnCorrectIntervalForUnevenCronSchedule()
    {
        // "0 0 0,8 * * ?" fires at midnight and 8am local time
        // From just after midnight, next fire is 8am (8h)
        // From just after 8am, next fire is midnight (16h)
        var cron = new Quartz.CronExpression("0 0 0,8 * * ?");
        var justAfterMidnight = cron.GetNextValidTimeAfter(DateTimeOffset.UtcNow)!.Value.AddSeconds(1);
        var justAfter8am = cron.GetNextValidTimeAfter(justAfterMidnight)!.Value.AddSeconds(1);

        var fromMidnight = ScheduleExtensions.GetExpectedIntervalSeconds("0 0 0,8 * * ?", justAfterMidnight);
        var from8am = ScheduleExtensions.GetExpectedIntervalSeconds("0 0 0,8 * * ?", justAfter8am);

        Assert.AreNotEqual(fromMidnight, from8am);
        Assert.IsTrue(Math.Min(fromMidnight, from8am) > 25000, $"Shorter interval should be ~8h but was {Math.Min(fromMidnight, from8am)}s");
        Assert.IsTrue(Math.Max(fromMidnight, from8am) > 50000, $"Longer interval should be ~16h but was {Math.Max(fromMidnight, from8am)}s");
    }

    [TestMethod]
    public void ToDisplayStringShouldReturnMinutesForEvenMinuteInterval()
    {
        var result = ScheduleExtensions.ToDisplayString("300");

        Assert.AreEqual("Every 5m", result);
    }

    [TestMethod]
    public void ToDisplayStringShouldReturnCronExpressionAsIs()
    {
        var result = ScheduleExtensions.ToDisplayString("0 */5 * * * ?");

        Assert.AreEqual("0 */5 * * * ?", result);
    }

    [TestMethod]
    public void ToDetailedDisplayStringShouldReturnMinutesForEvenMinuteInterval()
    {
        var result = ScheduleExtensions.ToDetailedDisplayString("300");

        Assert.AreEqual("Every 5 minute(s)", result);
    }

    [TestMethod]
    public void ToDetailedDisplayStringShouldReturnCronPrefixForCronExpression()
    {
        var result = ScheduleExtensions.ToDetailedDisplayString("0 */5 * * * ?");

        Assert.AreEqual("Cron: 0 */5 * * * ?", result);
    }
}
