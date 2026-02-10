using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using SAMA.Data.Entities;
using SAMA.Web.Services;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class DataCleanupJobIntegrationTests : IntegrationTestBase
{
    private DataCleanupJob _job = null!;
    private IJobExecutionContext _mockContext = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _job = new DataCleanupJob(ServiceProvider, Substitute.For<ILogger<DataCleanupJob>>());
        _mockContext = Substitute.For<IJobExecutionContext>();
        _mockContext.CancellationToken.Returns(CancellationToken.None);
    }

    [TestMethod]
    public async Task ExecuteShouldDeleteOldCheckResultsAndPreserveRecentOnes()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);

        var oldResult = await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow.AddDays(-400));
        var recentResult = await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow.AddDays(-100));

        await SetGlobalSettingAsync("CheckResultsRetentionDays", "365");

        await _job.Execute(_mockContext);

        var remainingResults = DbContext.CheckResults.ToList();

        Assert.HasCount(1, remainingResults);
        Assert.AreEqual(recentResult.Id, remainingResults[0].Id);
    }

    [TestMethod]
    public async Task ExecuteShouldDeleteOldAlertHistoryAndPreserveRecentOnes()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id);

        var oldHistory = await CreateAlertHistoryAsync(alert.Id, channel.Id, DateTimeOffset.UtcNow.AddDays(-400));
        var recentHistory = await CreateAlertHistoryAsync(alert.Id, channel.Id, DateTimeOffset.UtcNow.AddDays(-100));

        await SetGlobalSettingAsync("AlertHistoryRetentionDays", "365");

        await _job.Execute(_mockContext);

        var remainingHistory = DbContext.AlertHistories.ToList();

        Assert.HasCount(1, remainingHistory);
        Assert.AreEqual(recentHistory.Id, remainingHistory[0].Id);
    }

    [TestMethod]
    public async Task ExecuteShouldDeleteOldAuditLogsAndPreserveRecentOnes()
    {
        var oldLog = await CreateAuditLogAsync(DateTimeOffset.UtcNow.AddDays(-400));
        var recentLog = await CreateAuditLogAsync(DateTimeOffset.UtcNow.AddDays(-100));

        await SetGlobalSettingAsync("AuditLogRetentionDays", "365");

        await _job.Execute(_mockContext);

        var remainingLogs = DbContext.AuditLogs.ToList();

        Assert.HasCount(1, remainingLogs);
        Assert.AreEqual(recentLog.Id, remainingLogs[0].Id);
    }

    [TestMethod]
    public async Task ExecuteShouldRespectDifferentRetentionDaysForEachTable()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id);

        await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow.AddDays(-100));
        await CreateAlertHistoryAsync(alert.Id, channel.Id, DateTimeOffset.UtcNow.AddDays(-200));
        await CreateAuditLogAsync(DateTimeOffset.UtcNow.AddDays(-300));

        await SetGlobalSettingAsync("CheckResultsRetentionDays", "90");
        await SetGlobalSettingAsync("AlertHistoryRetentionDays", "180");
        await SetGlobalSettingAsync("AuditLogRetentionDays", "270");

        await _job.Execute(_mockContext);

        Assert.AreEqual(0, DbContext.CheckResults.Count());
        Assert.AreEqual(0, DbContext.AlertHistories.Count());
        Assert.AreEqual(0, DbContext.AuditLogs.Count());
    }

    [TestMethod]
    public async Task ExecuteShouldNotDeleteAnythingWhenNoOldData()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id);

        await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow.AddDays(-10));
        await CreateAlertHistoryAsync(alert.Id, channel.Id, DateTimeOffset.UtcNow.AddDays(-10));
        await CreateAuditLogAsync(DateTimeOffset.UtcNow.AddDays(-10));

        await SetGlobalSettingAsync("CheckResultsRetentionDays", "365");
        await SetGlobalSettingAsync("AlertHistoryRetentionDays", "365");
        await SetGlobalSettingAsync("AuditLogRetentionDays", "365");

        await _job.Execute(_mockContext);

        Assert.AreEqual(1, DbContext.CheckResults.Count());
        Assert.AreEqual(1, DbContext.AlertHistories.Count());
        Assert.AreEqual(1, DbContext.AuditLogs.Count());
    }

    [TestMethod]
    public async Task ExecuteShouldHandleVeryShortRetentionPeriod()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);

        await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow.AddDays(-2));
        await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow.AddHours(-12));

        await SetGlobalSettingAsync("CheckResultsRetentionDays", "1");

        await _job.Execute(_mockContext);

        var remainingResults = DbContext.CheckResults.ToList();

        Assert.HasCount(1, remainingResults);
    }

    [TestMethod]
    public async Task ExecuteShouldHandleMultipleRecordsWithSameCutoffDate()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-365);

        await CreateCheckResultAsync(check.Id, cutoffDate.AddHours(-1));
        await CreateCheckResultAsync(check.Id, cutoffDate.AddMinutes(-30));
        await CreateCheckResultAsync(check.Id, cutoffDate.AddMinutes(30));
        await CreateCheckResultAsync(check.Id, cutoffDate.AddHours(1));

        await SetGlobalSettingAsync("CheckResultsRetentionDays", "365");

        await _job.Execute(_mockContext);

        var remainingResults = DbContext.CheckResults.ToList();

        Assert.HasCount(2, remainingResults);
    }

    [TestMethod]
    public async Task ExecuteShouldHandleEmptyTables()
    {
        await SetGlobalSettingAsync("CheckResultsRetentionDays", "365");
        await SetGlobalSettingAsync("AlertHistoryRetentionDays", "365");
        await SetGlobalSettingAsync("AuditLogRetentionDays", "365");

        await _job.Execute(_mockContext);

        Assert.AreEqual(0, DbContext.CheckResults.Count());
        Assert.AreEqual(0, DbContext.AlertHistories.Count());
        Assert.AreEqual(0, DbContext.AuditLogs.Count());
    }

    [TestMethod]
    public async Task ExecuteShouldDeleteAllRecordsWhenRetentionIsZero()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);

        await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow.AddHours(-1));
        await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow);

        await SetGlobalSettingAsync("CheckResultsRetentionDays", "0");

        await _job.Execute(_mockContext);

        Assert.AreEqual(0, DbContext.CheckResults.Count());
    }

    [TestMethod]
    public async Task ExecuteShouldHandleLargeDataset()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);

        for (int i = 0; i < 100; i++)
        {
            await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow.AddDays(-400 + i));
        }

        await SetGlobalSettingAsync("CheckResultsRetentionDays", "365");

        await _job.Execute(_mockContext);

        var remainingResults = DbContext.CheckResults.ToList();

        Assert.IsLessThan(100, remainingResults.Count);
        Assert.IsTrue(remainingResults.All(r => r.CheckedAt >= DateTimeOffset.UtcNow.AddDays(-365)));
    }

    [TestMethod]
    public async Task ExecuteShouldPreserveDataIntegrityAcrossTables()
    {
        var workspace = await CreateWorkspaceAsync();
        var check = await CreateCheckAsync(workspace.Id);
        var channel = await CreateNotificationChannelAsync(workspace.Id);
        var alert = await CreateAlertAsync(check.Id);

        var oldCheckResult = await CreateCheckResultAsync(check.Id, DateTimeOffset.UtcNow.AddDays(-400));
        var oldAlertHistory = await CreateAlertHistoryAsync(alert.Id, channel.Id, DateTimeOffset.UtcNow.AddDays(-400));

        await SetGlobalSettingAsync("CheckResultsRetentionDays", "365");
        await SetGlobalSettingAsync("AlertHistoryRetentionDays", "365");

        await _job.Execute(_mockContext);

        var checkStillExists = DbContext.Checks.Any(c => c.Id == check.Id);
        var alertStillExists = DbContext.Alerts.Any(a => a.Id == alert.Id);
        var channelStillExists = DbContext.NotificationChannels.Any(nc => nc.Id == channel.Id);

        Assert.IsTrue(checkStillExists);
        Assert.IsTrue(alertStillExists);
        Assert.IsTrue(channelStillExists);
    }

    private async Task SetGlobalSettingAsync(string key, string value)
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
    }

    private async Task<Workspace> CreateWorkspaceAsync()
    {
        var workspace = new Workspace
        {
            Name = $"Test Workspace {Guid.NewGuid()}",
            IsPublic = false
        };

        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return workspace;
    }

    private async Task<Check> CreateCheckAsync(Guid workspaceId)
    {
        var check = new Check
        {
            WorkspaceId = workspaceId,
            Name = $"Test Check {Guid.NewGuid()}",
            CheckType = "Http",
            ConfigurationJson = new Dictionary<string, System.Text.Json.JsonElement>(),
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        DbContext.Checks.Add(check);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return check;
    }

    private async Task<CheckResult> CreateCheckResultAsync(Guid checkId, DateTimeOffset checkedAt)
    {
        var result = new CheckResult
        {
            CheckId = checkId,
            Status = "Up",
            ResponseTimeMs = 100,
            CheckedAt = checkedAt
        };

        DbContext.CheckResults.Add(result);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return result;
    }

    private async Task<NotificationChannel> CreateNotificationChannelAsync(Guid workspaceId)
    {
        var channel = new NotificationChannel
        {
            WorkspaceId = workspaceId,
            Name = $"Test Channel {Guid.NewGuid()}",
            ChannelType = "Email",
            ConfigurationJson = new Dictionary<string, System.Text.Json.JsonElement>(),
            Enabled = true
        };

        DbContext.NotificationChannels.Add(channel);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return channel;
    }

    private async Task<Alert> CreateAlertAsync(Guid checkId)
    {
        var alert = new Alert
        {
            CheckId = checkId,
            Name = $"Test Alert {Guid.NewGuid()}",
            TriggerOnWarn = true,
            TriggerOnDown = true,
            FailureThreshold = 1,
            SendRecoveryNotification = true,
            Enabled = true,
            NotificationChannels = []
        };

        DbContext.Alerts.Add(alert);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return alert;
    }

    private async Task<AlertHistory> CreateAlertHistoryAsync(Guid alertId, Guid channelId, DateTimeOffset sentAt)
    {
        var history = new AlertHistory
        {
            AlertId = alertId,
            NotificationChannelId = channelId,
            Status = "Down",
            Message = "Test alert",
            SentAt = sentAt,
            Success = true
        };

        DbContext.AlertHistories.Add(history);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return history;
    }

    private async Task<AuditLog> CreateAuditLogAsync(DateTimeOffset timestamp)
    {
        var log = new AuditLog
        {
            Action = "Created",
            EntityType = "Check",
            EntityId = Guid.NewGuid(),
            Timestamp = timestamp
        };

        DbContext.AuditLogs.Add(log);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return log;
    }
}
