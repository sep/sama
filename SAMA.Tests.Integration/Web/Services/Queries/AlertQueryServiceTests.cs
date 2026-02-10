using Microsoft.EntityFrameworkCore;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Integration.Web.Services.Queries;

[TestClass]
public class AlertQueryServiceTests : IntegrationTestBase
{
    private AlertQueryService _service = null!;
    private Workspace _workspace = null!;
    private Check _check = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _workspace = await CreateWorkspaceAsync("Test Workspace");
        _check = await CreateCheckAsync("Test Check", CheckTypes.Http, "60", true);
        _service = new AlertQueryService(DbContext);
    }

    [TestMethod]
    public async Task GetAlertsForCheckAsyncShouldReturnEmptyListWhenNoAlerts()
    {
        var result = await _service.GetAlertsForCheckAsync(_check.Id);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetAlertsForCheckAsyncShouldReturnAlertsOrderedByName()
    {
        await CreateAlertAsync(_check.Id, "Zebra Alert", true, true, 1, true, true);
        await CreateAlertAsync(_check.Id, "Alpha Alert", false, true, 2, false, false);
        await CreateAlertAsync(_check.Id, "Beta Alert", true, false, 3, true, true);

        var result = await _service.GetAlertsForCheckAsync(_check.Id);

        Assert.HasCount(3, result);
        Assert.AreEqual("Alpha Alert", result[0].Name);
        Assert.AreEqual("Beta Alert", result[1].Name);
        Assert.AreEqual("Zebra Alert", result[2].Name);
    }

    [TestMethod]
    public async Task GetAlertsForCheckAsyncShouldIncludeAlertProperties()
    {
        await CreateAlertAsync(_check.Id, "Test Alert", true, false, 5, false, true);

        var result = await _service.GetAlertsForCheckAsync(_check.Id);

        Assert.HasCount(1, result);
        var alert = result[0];
        Assert.AreEqual("Test Alert", alert.Name);
        Assert.IsTrue(alert.TriggerOnWarn);
        Assert.IsFalse(alert.TriggerOnDown);
        Assert.AreEqual(5, alert.FailureThreshold);
        Assert.IsFalse(alert.SendRecoveryNotification);
        Assert.IsTrue(alert.Enabled);
        Assert.IsTrue(alert.CreatedAt > DateTimeOffset.MinValue);
    }

    [TestMethod]
    public async Task GetAlertsForCheckAsyncShouldIncludeChannelCount()
    {
        var alert = await CreateAlertAsync(_check.Id, "Alert With Channels", true, true, 1, true, true);
        var channel1 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 1", "Email", true);
        var channel2 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 2", "Slack", true);

        var alertToUpdate = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);
        alertToUpdate.NotificationChannels.Add(channel1);
        alertToUpdate.NotificationChannels.Add(channel2);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.GetAlertsForCheckAsync(_check.Id);

        Assert.HasCount(1, result);
        Assert.AreEqual(2, result[0].ChannelCount);
    }

    [TestMethod]
    public async Task GetAlertsForCheckAsyncShouldOnlyReturnAlertsForSpecifiedCheck()
    {
        var otherCheck = await CreateCheckAsync("Other Check", CheckTypes.Tcp, "120", true);
        await CreateAlertAsync(_check.Id, "Check 1 Alert", true, true, 1, true, true);
        await CreateAlertAsync(otherCheck.Id, "Other Check Alert", true, true, 1, true, true);

        var result = await _service.GetAlertsForCheckAsync(_check.Id);

        Assert.HasCount(1, result);
        Assert.AreEqual("Check 1 Alert", result[0].Name);
    }

    [TestMethod]
    public async Task GetAlertDetailsAsyncShouldReturnNullWhenAlertDoesNotExist()
    {
        var result = await _service.GetAlertDetailsAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAlertDetailsAsyncShouldReturnAlertWithBasicProperties()
    {
        var alert = await CreateAlertAsync(_check.Id, "Details Alert", true, false, 3, true, false);

        var result = await _service.GetAlertDetailsAsync(alert.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(alert.Id, result.Id);
        Assert.AreEqual("Details Alert", result.Name);
        Assert.AreEqual(_check.Id, result.CheckId);
        Assert.AreEqual("Test Check", result.CheckName);
        Assert.AreEqual(_workspace.Id, result.WorkspaceId);
        Assert.AreEqual("Test Workspace", result.WorkspaceName);
        Assert.IsTrue(result.TriggerOnWarn);
        Assert.IsFalse(result.TriggerOnDown);
        Assert.AreEqual(3, result.FailureThreshold);
        Assert.IsTrue(result.SendRecoveryNotification);
        Assert.IsFalse(result.Enabled);
        Assert.IsTrue(result.CreatedAt > DateTimeOffset.MinValue);
        Assert.IsTrue(result.UpdatedAt > DateTimeOffset.MinValue);
    }

    [TestMethod]
    public async Task GetAlertDetailsAsyncShouldIncludeChannels()
    {
        var alert = await CreateAlertAsync(_check.Id, "Alert With Channels", true, true, 1, true, true);
        var channel1 = await CreateNotificationChannelAsync(_workspace.Id, "Email Channel", "Email", true);
        var channel2 = await CreateNotificationChannelAsync(_workspace.Id, "Slack Channel", "Slack", false);

        var alertToUpdate = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);
        alertToUpdate.NotificationChannels.Add(channel1);
        alertToUpdate.NotificationChannels.Add(channel2);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.GetAlertDetailsAsync(alert.Id);

        Assert.IsNotNull(result);
        Assert.HasCount(2, result.Channels);

        var emailChannel = result.Channels.First(c => c.Name == "Email Channel");
        Assert.AreEqual(channel1.Id, emailChannel.Id);
        Assert.AreEqual("Email", emailChannel.ChannelType);
        Assert.IsTrue(emailChannel.Enabled);

        var slackChannel = result.Channels.First(c => c.Name == "Slack Channel");
        Assert.AreEqual(channel2.Id, slackChannel.Id);
        Assert.AreEqual("Slack", slackChannel.ChannelType);
        Assert.IsFalse(slackChannel.Enabled);
    }

    [TestMethod]
    public async Task GetAlertDetailsAsyncShouldIncludeAlertHistoryCount()
    {
        var alert = await CreateAlertAsync(_check.Id, "Alert With History", true, true, 1, true, true);
        await CreateAlertHistoryAsync(alert.Id, "Down");
        await CreateAlertHistoryAsync(alert.Id, "Recovery");
        await CreateAlertHistoryAsync(alert.Id, "Warn");

        var result = await _service.GetAlertDetailsAsync(alert.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.AlertHistoryCount);
    }

    [TestMethod]
    public async Task GetAlertForEditAsyncShouldReturnNullWhenAlertDoesNotExist()
    {
        var result = await _service.GetAlertForEditAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetAlertForEditAsyncShouldReturnAlertEditViewModel()
    {
        var alert = await CreateAlertAsync(_check.Id, "Edit Alert", false, true, 2, false, true);

        var result = await _service.GetAlertForEditAsync(alert.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(alert.Id, result.Id);
        Assert.AreEqual("Edit Alert", result.Name);
        Assert.AreEqual(_check.Id, result.CheckId);
        Assert.AreEqual("Test Check", result.CheckName);
        Assert.AreEqual(_workspace.Id, result.WorkspaceId);
        Assert.AreEqual("Test Workspace", result.WorkspaceName);
        Assert.IsFalse(result.TriggerOnWarn);
        Assert.IsTrue(result.TriggerOnDown);
        Assert.AreEqual(2, result.FailureThreshold);
        Assert.IsFalse(result.SendRecoveryNotification);
        Assert.IsTrue(result.Enabled);
        Assert.IsNotNull(result.SelectedChannelIds);
    }

    [TestMethod]
    public async Task GetAlertForEditAsyncShouldIncludeSelectedChannelIds()
    {
        var alert = await CreateAlertAsync(_check.Id, "Alert With Selected Channels", true, true, 1, true, true);
        var channel1 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 1", "Email", true);
        var channel2 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 2", "Slack", true);

        var alertToUpdate = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);
        alertToUpdate.NotificationChannels.Add(channel1);
        alertToUpdate.NotificationChannels.Add(channel2);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.GetAlertForEditAsync(alert.Id);

        Assert.IsNotNull(result);
        Assert.HasCount(2, result.SelectedChannelIds);
        Assert.Contains(channel1.Id, result.SelectedChannelIds);
        Assert.Contains(channel2.Id, result.SelectedChannelIds);
    }

    [TestMethod]
    public async Task GetRecentAlertsForWorkspaceAsyncShouldReturnEmptyListWhenNoAlerts()
    {
        var result = await _service.GetRecentAlertsForWorkspaceAsync(_workspace.Id, 20);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetRecentAlertsForWorkspaceAsyncShouldGroupByTriggerEventId()
    {
        var channel1 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 1", "Email", true);
        var channel2 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 2", "Slack", true);
        var alert = await CreateAlertAsync(_check.Id, "Test Alert", true, true, 1, true, true);

        var triggerEventId1 = Guid.CreateVersion7();
        var triggerEventId2 = Guid.CreateVersion7();

        await CreateAlertHistoryAsync(alert.Id, channel1.Id, CheckStatuses.Down, triggerEventId1, success: true, sentAt: DateTimeOffset.UtcNow.AddMinutes(-10));
        await CreateAlertHistoryAsync(alert.Id, channel2.Id, CheckStatuses.Down, triggerEventId1, success: true, sentAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        await Task.Delay(10);

        await CreateAlertHistoryAsync(alert.Id, channel1.Id, CheckStatuses.Up, triggerEventId2, success: true, sentAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await CreateAlertHistoryAsync(alert.Id, channel2.Id, CheckStatuses.Up, triggerEventId2, success: true, sentAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        var result = await _service.GetRecentAlertsForWorkspaceAsync(_workspace.Id, 20);

        Assert.HasCount(2, result);
        Assert.AreEqual(CheckStatuses.Up, result[0].Status);
        Assert.AreEqual("Test Check", result[0].CheckName);
        Assert.AreEqual("Test Alert", result[0].AlertName);
        Assert.AreEqual(CheckStatuses.Down, result[1].Status);
    }

    [TestMethod]
    public async Task GetRecentAlertsForWorkspaceAsyncShouldRespectMaxAlertsLimit()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Test Alert", true, true, 1, true, true);

        for (int i = 0; i < 5; i++)
        {
            await Task.Delay(10);
            var triggerEventId = Guid.CreateVersion7();
            await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, triggerEventId, success: true);
        }

        var result = await _service.GetRecentAlertsForWorkspaceAsync(_workspace.Id, 3);

        Assert.HasCount(3, result);
    }

    [TestMethod]
    public async Task GetRecentAlertsForWorkspaceAsyncShouldOrderByMostRecent()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Test Alert", true, true, 1, true, true);

        var eventId1 = Guid.CreateVersion7();
        var eventId2 = Guid.CreateVersion7();
        var eventId3 = Guid.CreateVersion7();

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, eventId1, success: true, sentAt: DateTimeOffset.UtcNow.AddMinutes(-30));
        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Up, eventId2, success: true, sentAt: DateTimeOffset.UtcNow.AddMinutes(-20));
        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, eventId3, success: true, sentAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var result = await _service.GetRecentAlertsForWorkspaceAsync(_workspace.Id, 20);

        Assert.HasCount(3, result);
        Assert.AreEqual(CheckStatuses.Down, result[0].Status);
        Assert.AreEqual(CheckStatuses.Up, result[1].Status);
        Assert.AreEqual(CheckStatuses.Down, result[2].Status);
    }

    [TestMethod]
    public async Task GetRecentAlertsForWorkspaceAsyncShouldOnlyReturnAlertsForSpecifiedWorkspace()
    {
        var otherWorkspace = await CreateWorkspaceAsync("Other Workspace");
        var otherCheck = new Check
        {
            WorkspaceId = otherWorkspace.Id,
            Name = "Other Check",
            CheckType = CheckTypes.Http,
            ConfigurationJson = [],
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Checks.Add(otherCheck);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var channel1 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 1", "Email", true);
        var channel2 = await CreateNotificationChannelAsync(otherWorkspace.Id, "Channel 2", "Email", true);

        var alert1 = await CreateAlertAsync(_check.Id, "This Alert", true, true, 1, true, true);
        var alert2 = await CreateAlertAsync(otherCheck.Id, "Other Alert", true, true, 1, true, true);

        var eventId1 = Guid.CreateVersion7();
        var eventId2 = Guid.CreateVersion7();

        await CreateAlertHistoryAsync(alert1.Id, channel1.Id, CheckStatuses.Down, eventId1, success: true);
        await CreateAlertHistoryAsync(alert2.Id, channel2.Id, CheckStatuses.Down, eventId2, success: true);

        var result = await _service.GetRecentAlertsForWorkspaceAsync(_workspace.Id, 20);

        Assert.HasCount(1, result);
        Assert.AreEqual("Test Check", result[0].CheckName);
    }

    [TestMethod]
    public async Task GetRecentAlertsForWorkspaceAsyncShouldOnlyIncludeSuccessfulAlerts()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Test Alert", true, true, 1, true, true);

        var eventId1 = Guid.CreateVersion7();
        var eventId2 = Guid.CreateVersion7();

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, eventId1, success: true);
        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, eventId2, success: false);

        var result = await _service.GetRecentAlertsForWorkspaceAsync(_workspace.Id, 20);

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public async Task GetRecentAlertsForWorkspaceAsyncShouldIncludeCheckAndAlertNames()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Critical Alert", true, true, 1, true, true);
        var eventId = Guid.CreateVersion7();

        await CreateAlertHistoryAsync(alert.Id, channel.Id, CheckStatuses.Down, eventId, success: true);

        var result = await _service.GetRecentAlertsForWorkspaceAsync(_workspace.Id, 20);

        Assert.HasCount(1, result);
        Assert.AreEqual("Test Check", result[0].CheckName);
        Assert.AreEqual(_check.Id, result[0].CheckId);
        Assert.AreEqual("Critical Alert", result[0].AlertName);
        Assert.AreEqual(CheckStatuses.Down, result[0].Status);
        Assert.IsTrue(result[0].SentAt > DateTimeOffset.MinValue);
    }

    private async Task<Workspace> CreateWorkspaceAsync(string name)
    {
        var workspace = new Workspace
        {
            Name = name,
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return workspace;
    }

    private async Task<Check> CreateCheckAsync(string name, string checkType, string schedule, bool enabled)
    {
        var check = new Check
        {
            WorkspaceId = _workspace.Id,
            Name = name,
            CheckType = checkType,
            ConfigurationJson = [],
            Schedule = schedule,
            TimeoutSeconds = 30,
            Enabled = enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Checks.Add(check);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return check;
    }

    private async Task<Alert> CreateAlertAsync(
        Guid checkId,
        string name,
        bool triggerOnWarn,
        bool triggerOnDown,
        int failureThreshold,
        bool sendRecoveryNotification,
        bool enabled)
    {
        var alert = new Alert
        {
            CheckId = checkId,
            Name = name,
            TriggerOnWarn = triggerOnWarn,
            TriggerOnDown = triggerOnDown,
            FailureThreshold = failureThreshold,
            SendRecoveryNotification = sendRecoveryNotification,
            Enabled = enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.Alerts.Add(alert);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return alert;
    }

    private async Task<NotificationChannel> CreateNotificationChannelAsync(
        Guid workspaceId,
        string name,
        string channelType,
        bool enabled)
    {
        var channel = new NotificationChannel
        {
            WorkspaceId = workspaceId,
            Name = name,
            ChannelType = channelType,
            ConfigurationJson = [],
            Enabled = enabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        DbContext.NotificationChannels.Add(channel);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return channel;
    }

    private async Task<AlertHistory> CreateAlertHistoryAsync(Guid alertId, string notificationReason)
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "History Channel", "Email", true);

        var history = new AlertHistory
        {
            AlertId = alertId,
            NotificationChannelId = channel.Id,
            TriggerEventId = Guid.NewGuid(),
            Status = CheckStatuses.Down,
            Message = notificationReason,
            SentAt = DateTimeOffset.UtcNow,
            Success = true
        };

        DbContext.AlertHistories.Add(history);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return history;
    }

    private async Task<AlertHistory> CreateAlertHistoryAsync(
        Guid alertId,
        Guid channelId,
        string status,
        Guid triggerEventId,
        bool success,
        DateTimeOffset? sentAt = null)
    {
        var history = new AlertHistory
        {
            AlertId = alertId,
            NotificationChannelId = channelId,
            TriggerEventId = triggerEventId,
            Status = status,
            Message = $"Test message for {status}",
            Success = success,
            SentAt = sentAt ?? DateTimeOffset.UtcNow
        };

        DbContext.AlertHistories.Add(history);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return history;
    }
}
