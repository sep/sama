using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Web.Services;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class GlobalSettingsServiceTests : IntegrationTestBase
{
    private GlobalSettingsService _service = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _service = new GlobalSettingsService(ServiceProvider, Substitute.For<ILogger<GlobalSettingsService>>());
    }

    [TestMethod]
    public void CheckResultsRetentionDaysShouldReturnDefaultWhenNotInDatabase()
    {
        var value = _service.CheckResultsRetentionDays;

        Assert.AreEqual(365, value);
    }

    [TestMethod]
    public async Task CheckResultsRetentionDaysShouldReturnDatabaseValueWhenSet()
    {
        await CreateDbSettingAsync("CheckResultsRetentionDays", "180");

        var value = _service.CheckResultsRetentionDays;

        Assert.AreEqual(180, value);
    }

    [TestMethod]
    public async Task CheckResultsRetentionDaysShouldUpdateDatabaseAndCache()
    {
        _service.CheckResultsRetentionDays = 90;

        var setting = await DbContext.GlobalSettings.FindAsync("CheckResultsRetentionDays");
        Assert.IsNotNull(setting);
        Assert.AreEqual("90", setting.Value);

        var cachedValue = _service.CheckResultsRetentionDays;
        Assert.AreEqual(90, cachedValue);
    }

    [TestMethod]
    public void AlertHistoryRetentionDaysShouldReturnDefaultWhenNotInDatabase()
    {
        var value = _service.AlertHistoryRetentionDays;

        Assert.AreEqual(365, value);
    }

    [TestMethod]
    public async Task AlertHistoryRetentionDaysShouldReturnDatabaseValueWhenSet()
    {
        await CreateDbSettingAsync("AlertHistoryRetentionDays", "90");

        var value = _service.AlertHistoryRetentionDays;

        Assert.AreEqual(90, value);
    }

    [TestMethod]
    public async Task AlertHistoryRetentionDaysShouldUpdateDatabaseAndCache()
    {
        _service.AlertHistoryRetentionDays = 60;

        var setting = await DbContext.GlobalSettings.FindAsync("AlertHistoryRetentionDays");
        Assert.IsNotNull(setting);
        Assert.AreEqual("60", setting.Value);

        var cachedValue = _service.AlertHistoryRetentionDays;
        Assert.AreEqual(60, cachedValue);
    }

    [TestMethod]
    public void AuditLogRetentionDaysShouldReturnDefaultWhenNotInDatabase()
    {
        var value = _service.AuditLogRetentionDays;

        Assert.AreEqual(365, value);
    }

    [TestMethod]
    public async Task AuditLogRetentionDaysShouldReturnDatabaseValueWhenSet()
    {
        await CreateDbSettingAsync("AuditLogRetentionDays", "180");

        var value = _service.AuditLogRetentionDays;

        Assert.AreEqual(180, value);
    }

    [TestMethod]
    public async Task AuditLogRetentionDaysShouldUpdateDatabaseAndCache()
    {
        _service.AuditLogRetentionDays = 730;

        var setting = await DbContext.GlobalSettings.FindAsync("AuditLogRetentionDays");
        Assert.IsNotNull(setting);
        Assert.AreEqual("730", setting.Value);

        var cachedValue = _service.AuditLogRetentionDays;
        Assert.AreEqual(730, cachedValue);
    }

    [TestMethod]
    public void DashboardRefreshIntervalSecondsShouldReturnDefaultWhenNotInDatabase()
    {
        var value = _service.DashboardRefreshIntervalSeconds;

        Assert.AreEqual(5, value);
    }

    [TestMethod]
    public async Task DashboardRefreshIntervalSecondsShouldReturnDatabaseValueWhenSet()
    {
        await CreateDbSettingAsync("DashboardRefreshIntervalSeconds", "10");

        var value = _service.DashboardRefreshIntervalSeconds;

        Assert.AreEqual(10, value);
    }

    [TestMethod]
    public async Task DashboardRefreshIntervalSecondsShouldUpdateDatabaseAndCache()
    {
        _service.DashboardRefreshIntervalSeconds = 15;

        var setting = await DbContext.GlobalSettings.FindAsync("DashboardRefreshIntervalSeconds");
        Assert.IsNotNull(setting);
        Assert.AreEqual("15", setting.Value);

        var cachedValue = _service.DashboardRefreshIntervalSeconds;
        Assert.AreEqual(15, cachedValue);
    }

    [TestMethod]
    public void MaxRecentAlertsShouldReturnDefaultWhenNotInDatabase()
    {
        var value = _service.MaxRecentAlerts;

        Assert.AreEqual(20, value);
    }

    [TestMethod]
    public async Task MaxRecentAlertsShouldReturnDatabaseValueWhenSet()
    {
        await CreateDbSettingAsync("MaxRecentAlerts", "50");

        var value = _service.MaxRecentAlerts;

        Assert.AreEqual(50, value);
    }

    [TestMethod]
    public async Task MaxRecentAlertsShouldUpdateDatabaseAndCache()
    {
        _service.MaxRecentAlerts = 100;

        var setting = await DbContext.GlobalSettings.FindAsync("MaxRecentAlerts");
        Assert.IsNotNull(setting);
        Assert.AreEqual("100", setting.Value);

        var cachedValue = _service.MaxRecentAlerts;
        Assert.AreEqual(100, cachedValue);
    }

    [TestMethod]
    public void DefaultCheckTimeoutSecondsShouldReturnDefaultWhenNotInDatabase()
    {
        var value = _service.DefaultCheckTimeoutSeconds;

        Assert.AreEqual(30, value);
    }

    [TestMethod]
    public async Task DefaultCheckTimeoutSecondsShouldReturnDatabaseValueWhenSet()
    {
        await CreateDbSettingAsync("DefaultCheckTimeoutSeconds", "60");

        var value = _service.DefaultCheckTimeoutSeconds;

        Assert.AreEqual(60, value);
    }

    [TestMethod]
    public async Task DefaultCheckTimeoutSecondsShouldUpdateDatabaseAndCache()
    {
        _service.DefaultCheckTimeoutSeconds = 45;

        var setting = await DbContext.GlobalSettings.FindAsync("DefaultCheckTimeoutSeconds");
        Assert.IsNotNull(setting);
        Assert.AreEqual("45", setting.Value);

        var cachedValue = _service.DefaultCheckTimeoutSeconds;
        Assert.AreEqual(45, cachedValue);
    }

    [TestMethod]
    public void NotificationTimeoutSecondsShouldReturnDefaultWhenNotInDatabase()
    {
        var value = _service.NotificationTimeoutSeconds;

        Assert.AreEqual(30, value);
    }

    [TestMethod]
    public async Task NotificationTimeoutSecondsShouldReturnDatabaseValueWhenSet()
    {
        await CreateDbSettingAsync("NotificationTimeoutSeconds", "60");

        var value = _service.NotificationTimeoutSeconds;

        Assert.AreEqual(60, value);
    }

    [TestMethod]
    public async Task NotificationTimeoutSecondsShouldUpdateDatabaseAndCache()
    {
        _service.NotificationTimeoutSeconds = 45;

        var setting = await DbContext.GlobalSettings.FindAsync("NotificationTimeoutSeconds");
        Assert.IsNotNull(setting);
        Assert.AreEqual("45", setting.Value);

        var cachedValue = _service.NotificationTimeoutSeconds;
        Assert.AreEqual(45, cachedValue);
    }

    [TestMethod]
    public async Task ClearCacheShouldInvalidateCachedValues()
    {
        await CreateDbSettingAsync("MaxRecentAlerts", "50");

        var firstRead = _service.MaxRecentAlerts;
        Assert.AreEqual(50, firstRead);

        await UpdateDbSettingAsync("MaxRecentAlerts", "100");

        var beforeClear = _service.MaxRecentAlerts;
        Assert.AreEqual(50, beforeClear, "Should return cached value before clear");

        _service.ClearCache();

        var afterClear = _service.MaxRecentAlerts;
        Assert.AreEqual(100, afterClear, "Should return new value after cache clear");
    }

    [TestMethod]
    public async Task SettingShouldCacheValueAfterFirstLoad()
    {
        await CreateDbSettingAsync("DashboardRefreshIntervalSeconds", "10");

        var firstRead = _service.DashboardRefreshIntervalSeconds;

        await UpdateDbSettingAsync("DashboardRefreshIntervalSeconds", "20");

        var secondRead = _service.DashboardRefreshIntervalSeconds;

        Assert.AreEqual(10, secondRead, "Should return cached value, not updated database value");
    }

    [TestMethod]
    public async Task SetterShouldUpdateCacheImmediately()
    {
        await CreateDbSettingAsync("MaxRecentAlerts", "25");

        var initial = _service.MaxRecentAlerts;
        Assert.AreEqual(25, initial);

        _service.MaxRecentAlerts = 50;

        var afterSet = _service.MaxRecentAlerts;
        Assert.AreEqual(50, afterSet, "Should return new value immediately after setter");
    }

    [TestMethod]
    public async Task GetterShouldHandleInvalidDatabaseValue()
    {
        await CreateDbSettingAsync("CheckResultsRetentionDays", "not-a-number");

        var value = _service.CheckResultsRetentionDays;

        Assert.AreEqual(365, value, "Should return default when database value is invalid");
    }

    [TestMethod]
    public async Task MultipleSettingsShouldLoadIndependently()
    {
        await CreateDbSettingAsync("CheckResultsRetentionDays", "180");
        await CreateDbSettingAsync("MaxRecentAlerts", "50");

        var retention = _service.CheckResultsRetentionDays;
        var alerts = _service.MaxRecentAlerts;

        Assert.AreEqual(180, retention);
        Assert.AreEqual(50, alerts);
    }

    [TestMethod]
    public async Task UpdatedAtShouldBeSetWhenCreatingNewSetting()
    {
        var before = DateTimeOffset.UtcNow;

        _service.DashboardRefreshIntervalSeconds = 10;

        var after = DateTimeOffset.UtcNow;
        var setting = await DbContext.GlobalSettings.FindAsync("DashboardRefreshIntervalSeconds");

        Assert.IsNotNull(setting);
        Assert.IsTrue(setting.UpdatedAt >= before && setting.UpdatedAt <= after);
    }

    [TestMethod]
    public async Task UpdatedAtShouldBeUpdatedWhenModifyingExistingSetting()
    {
        await CreateDbSettingAsync("MaxRecentAlerts", "20");
        await Task.Delay(100);

        var before = DateTimeOffset.UtcNow;
        _service.MaxRecentAlerts = 50;
        var after = DateTimeOffset.UtcNow;

        var setting = await DbContext.GlobalSettings.FindAsync("MaxRecentAlerts");

        Assert.IsNotNull(setting);
        Assert.IsTrue(setting.UpdatedAt >= before && setting.UpdatedAt <= after);
    }

    [TestMethod]
    public void TimeZoneShouldReturnDefaultWhenNotInDatabase()
    {
        var value = _service.TimeZone;

        Assert.AreEqual("UTC", value);
    }

    [TestMethod]
    public async Task TimeZoneShouldReturnDatabaseValueWhenSet()
    {
        await CreateDbSettingAsync("TimeZone", "America/New_York");

        var value = _service.TimeZone;

        Assert.AreEqual("America/New_York", value);
    }

    [TestMethod]
    public async Task TimeZoneShouldUpdateDatabaseAndCache()
    {
        _service.TimeZone = "Europe/London";

        var setting = await DbContext.GlobalSettings.FindAsync("TimeZone");
        Assert.IsNotNull(setting);
        Assert.AreEqual("Europe/London", setting.Value);

        var cachedValue = _service.TimeZone;
        Assert.AreEqual("Europe/London", cachedValue);
    }

    private async Task<GlobalSetting> CreateDbSettingAsync(string key, string value)
    {
        var setting = new GlobalSetting
        {
            Key = key,
            Value = value,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.GlobalSettings.Add(setting);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return setting;
    }

    private async Task UpdateDbSettingAsync(string key, string value)
    {
        var setting = await DbContext.GlobalSettings.FindAsync(key);
        Assert.IsNotNull(setting);
        setting.Value = value;
        setting.UpdatedAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }
}
