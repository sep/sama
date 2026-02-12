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
    public void ToDisplayStringShouldReturnPluralMinutes()
    {
        var result = ScheduleExtensions.ToDisplayString("300");

        Assert.AreEqual("Every 5 minutes", result);
    }

    [TestMethod]
    public void ToDisplayStringShouldReturnSingularMinute()
    {
        var result = ScheduleExtensions.ToDisplayString("60");

        Assert.AreEqual("Every minute", result);
    }

    [TestMethod]
    public void ToDisplayStringShouldReturnPluralHours()
    {
        var result = ScheduleExtensions.ToDisplayString("7200");

        Assert.AreEqual("Every 2 hours", result);
    }

    [TestMethod]
    public void ToDisplayStringShouldReturnSingularDay()
    {
        var result = ScheduleExtensions.ToDisplayString("86400");

        Assert.AreEqual("Every day", result);
    }

    [TestMethod]
    public void ToDisplayStringShouldReturnFriendlyDescriptionForCron()
    {
        var result = ScheduleExtensions.ToDisplayString("0 */5 * * * ?");

        Assert.AreEqual("Every 5 minutes", result);
    }

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldAcceptTimeZoneParameter()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var result = ScheduleExtensions.GetExpectedIntervalSeconds("0 */5 * * * ?", ReferenceTime, tz);

        Assert.AreEqual(300, result);
    }

    [TestMethod]
    public void GetExpectedIntervalSecondsShouldIgnoreTimeZoneForNumericSchedule()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var result = ScheduleExtensions.GetExpectedIntervalSeconds("60", ReferenceTime, tz);

        Assert.AreEqual(60, result);
    }

    [TestMethod]
    public void IsCronExpressionShouldReturnFalseForNumericSchedule()
    {
        Assert.IsFalse(ScheduleExtensions.IsCronExpression("300"));
    }

    [TestMethod]
    public void IsCronExpressionShouldReturnTrueForCronSchedule()
    {
        Assert.IsTrue(ScheduleExtensions.IsCronExpression("0 */5 * * * ?"));
    }

    [TestMethod]
    public void GetCronDescriptionShouldReturnHumanReadableDescription()
    {
        var result = ScheduleExtensions.GetCronDescription("0 */5 * * * ?");

        Assert.AreEqual("Every 5 minutes", result);
    }

    [TestMethod]
    public void GetCronDescriptionShouldReturnInputForInvalidExpression()
    {
        var result = ScheduleExtensions.GetCronDescription("not-valid");

        Assert.AreEqual("not-valid", result);
    }

    [TestMethod]
    public void ValidateScheduleShouldReturnNullForValidInterval()
    {
        Assert.IsNull(ScheduleExtensions.ValidateSchedule("60"));
    }

    [TestMethod]
    public void ValidateScheduleShouldReturnNullForMaxInterval()
    {
        Assert.IsNull(ScheduleExtensions.ValidateSchedule("86400"));
    }

    [TestMethod]
    public void ValidateScheduleShouldReturnErrorForIntervalTooLow()
    {
        var result = ScheduleExtensions.ValidateSchedule("5");

        Assert.IsNotNull(result);
        StringAssert.Contains(result, "at least");
    }

    [TestMethod]
    public void ValidateScheduleShouldReturnErrorForIntervalTooHigh()
    {
        var result = ScheduleExtensions.ValidateSchedule("86401");

        Assert.IsNotNull(result);
        StringAssert.Contains(result, "cannot exceed");
    }

    [TestMethod]
    public void ValidateScheduleShouldReturnNullForValidCron()
    {
        Assert.IsNull(ScheduleExtensions.ValidateSchedule("0 */5 * * * ?"));
    }

    [TestMethod]
    public void ValidateScheduleShouldReturnErrorForInvalidCron()
    {
        var result = ScheduleExtensions.ValidateSchedule("not-valid");

        Assert.IsNotNull(result);
        StringAssert.Contains(result, "Invalid schedule");
    }
}
