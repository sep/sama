using SAMA.Web.Extensions;

namespace SAMA.Tests.Unit.Web.Extensions;

[TestClass]
public class TimeZoneExtensionsTests
{
    [TestMethod]
    public void GetIanaTimeZonesShouldReturnNonEmptyList()
    {
        var timeZones = TimeZoneExtensions.GetIanaTimeZones();

        Assert.IsTrue(timeZones.Count > 0);
    }

    [TestMethod]
    public void GetIanaTimeZonesShouldContainUtc()
    {
        var timeZones = TimeZoneExtensions.GetIanaTimeZones();

        Assert.IsTrue(timeZones.Any(tz => tz.IanaId == "UTC" || tz.IanaId == "Etc/UTC"));
    }

    [TestMethod]
    public void GetIanaTimeZonesShouldHaveUniqueIds()
    {
        var timeZones = TimeZoneExtensions.GetIanaTimeZones();
        var uniqueIds = timeZones.Select(tz => tz.IanaId).Distinct().ToList();

        Assert.AreEqual(uniqueIds.Count, timeZones.Count);
    }

    [TestMethod]
    public void GetIanaTimeZonesShouldBeOrderedByDisplayName()
    {
        var timeZones = TimeZoneExtensions.GetIanaTimeZones();
        var displayNames = timeZones.Select(tz => tz.DisplayName).ToList();
        var sorted = displayNames.OrderBy(n => n).ToList();

        CollectionAssert.AreEqual(sorted, displayNames);
    }

    [TestMethod]
    public void FindTimeZoneByIanaIdShouldReturnUtc()
    {
        var tz = TimeZoneExtensions.FindTimeZoneByIanaId("UTC");

        Assert.AreEqual(TimeSpan.Zero, tz.BaseUtcOffset);
    }

    [TestMethod]
    public void FindTimeZoneByIanaIdShouldReturnNewYork()
    {
        var tz = TimeZoneExtensions.FindTimeZoneByIanaId("America/New_York");

        Assert.AreEqual(TimeSpan.FromHours(-5), tz.BaseUtcOffset);
    }

    [TestMethod]
    public void FindTimeZoneByIanaIdShouldThrowForInvalidId()
    {
        Assert.ThrowsExactly<TimeZoneNotFoundException>(
            () => TimeZoneExtensions.FindTimeZoneByIanaId("Invalid/TimeZone"));
    }
}
