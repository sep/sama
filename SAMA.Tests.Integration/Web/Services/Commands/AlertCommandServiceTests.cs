using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;

namespace SAMA.Tests.Integration.Web.Services.Commands;

[TestClass]
public class AlertCommandServiceTests : IntegrationTestBase
{
    private AlertCommandService _service = null!;
    private CheckSchedulerService _mockScheduler = null!;
    private EventSubscriptionService _mockEventService = null!;
    private Workspace _workspace = null!;
    private Check _check = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _workspace = await CreateWorkspaceAsync("Test Workspace");
        _check = await CreateCheckAsync("Test Check", CheckTypes.Http, "60", true);
        _mockScheduler = Substitute.For<CheckSchedulerService>(null, null, null);
        _mockEventService = Substitute.For<EventSubscriptionService>(null, null, null);

        _service = new AlertCommandService(
            DbContext,
            _mockScheduler,
            _mockEventService,
            new AlertChangeDetectionService(),
            new DashboardCacheService(ServiceProvider),
            Substitute.For<ILogger<AlertCommandService>>());
    }

    [TestMethod]
    public async Task CreateAlertAsyncShouldReturnFailureWhenCheckDoesNotExist()
    {
        var result = await _service.CreateAlertAsync(
            Guid.NewGuid(),
            "Test Alert",
            true,
            true,
            1,
            true,
            true,
            [],
            "admin");

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Check not found", result.ErrorMessage);
    }

    [TestMethod]
    public async Task CreateAlertAsyncShouldCreateAlertWithBasicProperties()
    {
        var result = await _service.CreateAlertAsync(
            _check.Id,
            "New Alert",
            true,
            false,
            3,
            false,
            true,
            [],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.AreNotEqual(Guid.Empty, result.AlertId);

        var alert = await DbContext.Alerts.FindAsync(result.AlertId);
        Assert.IsNotNull(alert);
        Assert.AreEqual("New Alert", alert.Name);
        Assert.AreEqual(_check.Id, alert.CheckId);
        Assert.IsTrue(alert.TriggerOnWarn);
        Assert.IsFalse(alert.TriggerOnDown);
        Assert.AreEqual(3, alert.FailureThreshold);
        Assert.IsFalse(alert.SendRecoveryNotification);
        Assert.IsTrue(alert.Enabled);
    }

    [TestMethod]
    public async Task CreateAlertAsyncShouldAttachSelectedChannels()
    {
        var channel1 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 1", "Email", true);
        var channel2 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 2", "Slack", true);

        var result = await _service.CreateAlertAsync(
            _check.Id,
            "Alert With Channels",
            true,
            true,
            1,
            true,
            true,
            [channel1.Id, channel2.Id],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.ChannelCount);

        DbContext.ChangeTracker.Clear();
        var alert = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == result.AlertId);

        Assert.HasCount(2, alert.NotificationChannels);
        Assert.IsTrue(alert.NotificationChannels.Any(c => c.Id == channel1.Id));
        Assert.IsTrue(alert.NotificationChannels.Any(c => c.Id == channel2.Id));
    }

    [TestMethod]
    public async Task CreateAlertAsyncShouldTriggerCheckWhenEnabledWithChannels()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);

        var result = await _service.CreateAlertAsync(
            _check.Id,
            "Enabled Alert",
            true,
            true,
            1,
            true,
            true,
            [channel.Id],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.ShouldTriggerCheck);
        await _mockScheduler.Received(1).TriggerImmediateCheckAsync(_check.Id);
    }

    [TestMethod]
    public async Task CreateAlertAsyncShouldTriggerCheckWhenEnabledWithWorkspaceChannels()
    {
        await CreateNotificationChannelAsync(_workspace.Id, "Workspace Channel", "Email", true);

        var result = await _service.CreateAlertAsync(
            _check.Id,
            "Alert Using All Channels",
            true,
            true,
            1,
            true,
            true,
            [],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.ShouldTriggerCheck);
        Assert.AreEqual(0, result.ChannelCount);
        Assert.AreEqual(1, result.AllChannelsCount);
        await _mockScheduler.Received(1).TriggerImmediateCheckAsync(_check.Id);
    }

    [TestMethod]
    public async Task CreateAlertAsyncShouldNotTriggerCheckWhenDisabled()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);

        var result = await _service.CreateAlertAsync(
            _check.Id,
            "Disabled Alert",
            true,
            true,
            1,
            true,
            false,
            [channel.Id],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.ShouldTriggerCheck);
        await _mockScheduler.DidNotReceive().TriggerImmediateCheckAsync(Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task CreateAlertAsyncShouldNotTriggerCheckWhenNoChannelsExist()
    {
        var result = await _service.CreateAlertAsync(
            _check.Id,
            "Alert Without Channels",
            true,
            true,
            1,
            true,
            true,
            [],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.ShouldTriggerCheck);
        await _mockScheduler.DidNotReceive().TriggerImmediateCheckAsync(Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task CreateAlertAsyncShouldTriggerLifecycleEvent()
    {
        var result = await _service.CreateAlertAsync(
            _check.Id,
            "Event Alert",
            true,
            true,
            1,
            true,
            true,
            [],
            "testuser");

        Assert.IsTrue(result.Success);

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.EventType == EventTypes.CheckUpdated &&
                ctx.CheckId == _check.Id &&
                ctx.CheckName == "Test Check" &&
                ctx.CheckType == CheckTypes.Http &&
                ctx.WorkspaceName == "Test Workspace" &&
                ctx.PerformedBy == "testuser" &&
                ctx.ConfigurationChanges != null &&
                ctx.ConfigurationChanges.ContainsKey("Alert Created") &&
                (string)ctx.ConfigurationChanges["Alert Created"] == "Event Alert" &&
                ctx.ConfigurationChanges.ContainsKey("Triggers") &&
                ctx.ConfigurationChanges.ContainsKey("Failure Threshold") &&
                ctx.ConfigurationChanges.ContainsKey("Notification Channels")),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldReturnFailureWhenAlertDoesNotExist()
    {
        var result = await _service.UpdateAlertAsync(
            Guid.NewGuid(),
            "Updated Alert",
            true,
            true,
            1,
            true,
            true,
            [],
            "admin");

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Alert not found", result.ErrorMessage);
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldUpdateAlertProperties()
    {
        var alert = await CreateAlertAsync(_check.Id, "Original Alert", true, true, 1, true, true);

        var result = await _service.UpdateAlertAsync(
            alert.Id,
            "Updated Alert",
            false,
            true,
            5,
            false,
            false,
            [],
            "admin");

        Assert.IsTrue(result.Success);

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Alerts.FindAsync(alert.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual("Updated Alert", updated.Name);
        Assert.IsFalse(updated.TriggerOnWarn);
        Assert.IsTrue(updated.TriggerOnDown);
        Assert.AreEqual(5, updated.FailureThreshold);
        Assert.IsFalse(updated.SendRecoveryNotification);
        Assert.IsFalse(updated.Enabled);
        Assert.IsTrue(updated.UpdatedAt > alert.UpdatedAt);
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldUpdateChannels()
    {
        var channel1 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 1", "Email", true);
        var channel2 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 2", "Slack", true);
        var channel3 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 3", "Webhook", true);

        var alert = await CreateAlertAsync(_check.Id, "Alert", true, true, 1, true, true);
        var alertToUpdate = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);
        alertToUpdate.NotificationChannels.Add(channel1);
        alertToUpdate.NotificationChannels.Add(channel2);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.UpdateAlertAsync(
            alert.Id,
            "Updated Alert",
            true,
            true,
            1,
            true,
            true,
            [channel2.Id, channel3.Id],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.ChannelCount);

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);

        Assert.HasCount(2, updated.NotificationChannels);
        Assert.IsFalse(updated.NotificationChannels.Any(c => c.Id == channel1.Id));
        Assert.IsTrue(updated.NotificationChannels.Any(c => c.Id == channel2.Id));
        Assert.IsTrue(updated.NotificationChannels.Any(c => c.Id == channel3.Id));
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldTriggerCheckWhenEnablingAlertWithChannels()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Disabled Alert", true, true, 1, true, false);

        var result = await _service.UpdateAlertAsync(
            alert.Id,
            "Enabled Alert",
            true,
            true,
            1,
            true,
            true,
            [channel.Id],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.ShouldTriggerCheck);
        await _mockScheduler.Received(1).TriggerImmediateCheckAsync(_check.Id);
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldTriggerCheckWhenAddingChannelsToEnabledAlert()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Alert Without Channels", true, true, 1, true, true);

        var result = await _service.UpdateAlertAsync(
            alert.Id,
            "Alert With Channels",
            true,
            true,
            1,
            true,
            true,
            [channel.Id],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.ShouldTriggerCheck);
        await _mockScheduler.Received(1).TriggerImmediateCheckAsync(_check.Id);
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldNotTriggerCheckWhenAlertAlreadyEnabledWithChannels()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Alert", true, true, 1, true, true);
        var alertToUpdate = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);
        alertToUpdate.NotificationChannels.Add(channel);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.UpdateAlertAsync(
            alert.Id,
            "Updated Alert",
            false,
            true,
            2,
            false,
            true,
            [channel.Id],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.ShouldTriggerCheck);
        await _mockScheduler.DidNotReceive().TriggerImmediateCheckAsync(Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldNotTriggerCheckWhenDisablingAlert()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Enabled Alert", true, true, 1, true, true);
        var alertToUpdate = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);
        alertToUpdate.NotificationChannels.Add(channel);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.UpdateAlertAsync(
            alert.Id,
            "Disabled Alert",
            true,
            true,
            1,
            true,
            false,
            [channel.Id],
            "admin");

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.ShouldTriggerCheck);
        await _mockScheduler.DidNotReceive().TriggerImmediateCheckAsync(Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldTriggerLifecycleEvent()
    {
        var alert = await CreateAlertAsync(_check.Id, "Original Alert", true, true, 1, true, true);

        var result = await _service.UpdateAlertAsync(
            alert.Id,
            "Updated Alert",
            false,
            true,
            2,
            false,
            false,
            [],
            "testuser");

        Assert.IsTrue(result.Success);

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.EventType == EventTypes.CheckUpdated &&
                ctx.CheckId == _check.Id &&
                ctx.CheckName == "Test Check" &&
                ctx.CheckType == CheckTypes.Http &&
                ctx.WorkspaceName == "Test Workspace" &&
                ctx.PerformedBy == "testuser" &&
                ctx.ConfigurationChanges != null &&
                ctx.ConfigurationChanges.ContainsKey("Alert 'Original Alert' (renamed to 'Updated Alert'): Name") &&
                ctx.ConfigurationChanges.ContainsKey("Alert 'Original Alert' (renamed to 'Updated Alert'): Trigger on Degraded") &&
                ctx.ConfigurationChanges.ContainsKey("Alert 'Original Alert' (renamed to 'Updated Alert'): Failure Threshold") &&
                ctx.ConfigurationChanges.ContainsKey("Alert 'Original Alert' (renamed to 'Updated Alert'): Send Recovery Notification") &&
                ctx.ConfigurationChanges.ContainsKey("Alert 'Original Alert' (renamed to 'Updated Alert'): Enabled") &&
                ctx.ConfigurationChanges.ContainsKey("Alert 'Original Alert' (renamed to 'Updated Alert'): Updated At")),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldIncludeChannelChangesInLifecycleEvent()
    {
        var channel1 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 1", "Email", true);
        var channel2 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 2", "Slack", true);
        var channel3 = await CreateNotificationChannelAsync(_workspace.Id, "Channel 3", "Teams", true);

        var alert = await CreateAlertAsync(_check.Id, "Alert", true, true, 1, true, true);
        var alertToUpdate = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);
        alertToUpdate.NotificationChannels.Add(channel1);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.UpdateAlertAsync(
            alert.Id,
            "Alert",
            true,
            true,
            1,
            true,
            true,
            [channel2.Id, channel3.Id],
            "testuser");

        Assert.IsTrue(result.Success);

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.ConfigurationChanges != null &&
                ctx.ConfigurationChanges.ContainsKey("Alert 'Alert': Notification Channels") &&
                ((string)ctx.ConfigurationChanges["Alert 'Alert': Notification Channels"]).Contains("added") &&
                ((string)ctx.ConfigurationChanges["Alert 'Alert': Notification Channels"]).Contains("removed")),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task UpdateAlertAsyncShouldIncludeOnlyUpdatedAtWhenNoFieldsChange()
    {
        var alert = await CreateAlertAsync(_check.Id, "Test Alert", true, true, 1, true, true);

        var result = await _service.UpdateAlertAsync(
            alert.Id,
            "Test Alert",
            true,
            true,
            1,
            true,
            true,
            [],
            "testuser");

        Assert.IsTrue(result.Success);

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.ConfigurationChanges != null &&
                ctx.ConfigurationChanges.ContainsKey("Alert 'Test Alert': Updated At") &&
                ctx.ConfigurationChanges.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task DeleteAlertAsyncShouldReturnFalseWhenAlertDoesNotExist()
    {
        var result = await _service.DeleteAlertAsync(Guid.NewGuid(), "admin");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DeleteAlertAsyncShouldDeleteAlert()
    {
        var alert = await CreateAlertAsync(_check.Id, "Alert To Delete", true, true, 1, true, true);

        var result = await _service.DeleteAlertAsync(alert.Id, "admin");

        Assert.IsTrue(result);

        var deleted = await DbContext.Alerts.FindAsync(alert.Id);
        Assert.IsNull(deleted);
    }

    [TestMethod]
    public async Task DeleteAlertAsyncShouldDeleteAlertWithChannels()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Alert With Channel", true, true, 1, true, true);
        var alertToUpdate = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);
        alertToUpdate.NotificationChannels.Add(channel);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.DeleteAlertAsync(alert.Id, "admin");

        Assert.IsTrue(result);

        var deleted = await DbContext.Alerts.FindAsync(alert.Id);
        Assert.IsNull(deleted);

        var channelStillExists = await DbContext.NotificationChannels.FindAsync(channel.Id);
        Assert.IsNotNull(channelStillExists);
    }

    [TestMethod]
    public async Task DeleteAlertAsyncShouldDeleteRelatedAlertHistory()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var alert = await CreateAlertAsync(_check.Id, "Alert With History", true, true, 1, true, true);

        var history = new AlertHistory
        {
            AlertId = alert.Id,
            NotificationChannelId = channel.Id,
            TriggerEventId = Guid.NewGuid(),
            Status = CheckStatuses.Down,
            Message = "Test",
            SentAt = DateTimeOffset.UtcNow,
            Success = true
        };
        DbContext.AlertHistories.Add(history);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await _service.DeleteAlertAsync(alert.Id, "admin");

        var deletedHistory = await DbContext.AlertHistories.FindAsync(history.Id);
        Assert.IsNull(deletedHistory);
    }

    [TestMethod]
    public async Task DeleteAlertAsyncShouldTriggerLifecycleEvent()
    {
        var alert = await CreateAlertAsync(_check.Id, "Alert To Delete", true, true, 1, true, true);
        var alertId = alert.Id;

        var result = await _service.DeleteAlertAsync(alertId, "testuser");

        Assert.IsTrue(result);

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.EventType == EventTypes.CheckUpdated &&
                ctx.CheckId == _check.Id &&
                ctx.CheckName == "Test Check" &&
                ctx.CheckType == CheckTypes.Http &&
                ctx.WorkspaceName == "Test Workspace" &&
                ctx.PerformedBy == "testuser" &&
                ctx.ConfigurationChanges != null &&
                ctx.ConfigurationChanges.ContainsKey("Alert Deleted") &&
                (string)ctx.ConfigurationChanges["Alert Deleted"] == "Alert To Delete"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task CreateAlertAsyncShouldSupportMultipleAlertsForSameCheck()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);

        var result1 = await _service.CreateAlertAsync(
            _check.Id, "Alert 1", true, true, 1, true, true, [channel.Id], "admin");

        var result2 = await _service.CreateAlertAsync(
            _check.Id, "Alert 2", false, true, 2, false, false, [], "admin");

        Assert.IsTrue(result1.Success);
        Assert.IsTrue(result2.Success);

        var alerts = DbContext.Alerts.Where(a => a.CheckId == _check.Id).ToList();
        Assert.HasCount(2, alerts);
        Assert.IsTrue(alerts.Any(a => a.Name == "Alert 1"));
        Assert.IsTrue(alerts.Any(a => a.Name == "Alert 2"));
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
}
