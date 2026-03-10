using SAMA.Data.Entities;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class AlertChangeDetectionServiceTests
{
    private AlertChangeDetectionService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = new AlertChangeDetectionService();
    }

    #region DetectChanges Tests

    [TestMethod]
    public void DetectChangesShouldDetectAlertNameChange()
    {
        var oldAlert = CreateAlert("Old Alert");
        var changes = _service.DetectChanges(
            oldAlert,
            "New Alert",
            oldAlert.TriggerOnWarn,
            oldAlert.TriggerOnDown,
            oldAlert.FailureThreshold,
            oldAlert.SendRecoveryNotification,
            oldAlert.Enabled,
            []);

        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Name"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectTriggerOnWarnChange()
    {
        var oldAlert = CreateAlert("Test", triggerOnWarn: false);
        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            true,
            oldAlert.TriggerOnDown,
            oldAlert.FailureThreshold,
            oldAlert.SendRecoveryNotification,
            oldAlert.Enabled,
            []);

        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Trigger on Degraded"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectTriggerOnDownChange()
    {
        var oldAlert = CreateAlert("Test", triggerOnDown: true);
        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            oldAlert.TriggerOnWarn,
            false,
            oldAlert.FailureThreshold,
            oldAlert.SendRecoveryNotification,
            oldAlert.Enabled,
            []);

        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Trigger on Down"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectFailureThresholdChange()
    {
        var oldAlert = CreateAlert("Test", failureThreshold: 1);
        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            oldAlert.TriggerOnWarn,
            oldAlert.TriggerOnDown,
            5,
            oldAlert.SendRecoveryNotification,
            oldAlert.Enabled,
            []);

        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Failure Threshold"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectSendRecoveryNotificationChange()
    {
        var oldAlert = CreateAlert("Test", sendRecoveryNotification: true);
        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            oldAlert.TriggerOnWarn,
            oldAlert.TriggerOnDown,
            oldAlert.FailureThreshold,
            false,
            oldAlert.Enabled,
            []);

        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Send Recovery Notification"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectEnabledChange()
    {
        var oldAlert = CreateAlert("Test", enabled: true);
        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            oldAlert.TriggerOnWarn,
            oldAlert.TriggerOnDown,
            oldAlert.FailureThreshold,
            oldAlert.SendRecoveryNotification,
            false,
            []);

        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Enabled"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldAlwaysIncludeUpdatedAt()
    {
        var oldAlert = CreateAlert("Test");
        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            oldAlert.TriggerOnWarn,
            oldAlert.TriggerOnDown,
            oldAlert.FailureThreshold,
            oldAlert.SendRecoveryNotification,
            oldAlert.Enabled,
            []);

        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
        Assert.HasCount(1, changes);
    }

    [TestMethod]
    public void DetectChangesShouldDetectChannelsAdded()
    {
        var oldAlert = CreateAlert("Test");
        var newChannelIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            oldAlert.TriggerOnWarn,
            oldAlert.TriggerOnDown,
            oldAlert.FailureThreshold,
            oldAlert.SendRecoveryNotification,
            oldAlert.Enabled,
            newChannelIds);

        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Notification Channels"));
        Assert.Contains("2 added", changes["Alert 'Test': Notification Channels"].ToString()!);
        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectChannelsRemoved()
    {
        var channel1 = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Channel 1",
            ChannelType = "Email",
            ConfigurationJson = []
        };
        var channel2 = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Channel 2",
            ChannelType = "Slack",
            ConfigurationJson = []
        };
        var oldAlert = CreateAlert("Test", channels: [channel1, channel2]);

        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            oldAlert.TriggerOnWarn,
            oldAlert.TriggerOnDown,
            oldAlert.FailureThreshold,
            oldAlert.SendRecoveryNotification,
            oldAlert.Enabled,
            []);

        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Notification Channels"));
        Assert.Contains(changes["Alert 'Test': Notification Channels"].ToString()!, "2 removed");
        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldDetectChannelsAddedAndRemoved()
    {
        var oldChannel1 = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Old Channel 1",
            ChannelType = "Email",
            ConfigurationJson = []
        };
        var oldChannel2 = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Old Channel 2",
            ChannelType = "Slack",
            ConfigurationJson = []
        };
        var oldAlert = CreateAlert("Test", channels: [oldChannel1, oldChannel2]);

        var newChannelId1 = Guid.NewGuid();
        var newChannelId2 = Guid.NewGuid();
        var newChannelId3 = Guid.NewGuid();
        var newChannelIds = new List<Guid> { oldChannel1.Id, newChannelId1, newChannelId2, newChannelId3 };

        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            oldAlert.TriggerOnWarn,
            oldAlert.TriggerOnDown,
            oldAlert.FailureThreshold,
            oldAlert.SendRecoveryNotification,
            oldAlert.Enabled,
            newChannelIds);

        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Notification Channels"));
        var channelChanges = changes["Alert 'Test': Notification Channels"].ToString()!;
        Assert.Contains("3 added", channelChanges);
        Assert.Contains("1 removed", channelChanges);
        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
    }

    [TestMethod]
    public void DetectChangesShouldNotDetectChannelChangesWhenChannelsAreSame()
    {
        var channel1 = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Channel 1",
            ChannelType = "Email",
            ConfigurationJson = []
        };
        var channel2 = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Channel 2",
            ChannelType = "Slack",
            ConfigurationJson = []
        };
        var oldAlert = CreateAlert("Test", channels: [channel1, channel2]);

        var changes = _service.DetectChanges(
            oldAlert,
            oldAlert.Name,
            oldAlert.TriggerOnWarn,
            oldAlert.TriggerOnDown,
            oldAlert.FailureThreshold,
            oldAlert.SendRecoveryNotification,
            oldAlert.Enabled,
            [channel1.Id, channel2.Id]);

        Assert.IsFalse(changes.ContainsKey("Alert 'Test': Notification Channels"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Test': Updated At"));
        Assert.HasCount(1, changes);
    }

    [TestMethod]
    public void DetectChangesShouldHandleMultipleChanges()
    {
        var oldAlert = CreateAlert("Old Alert", triggerOnWarn: true, triggerOnDown: true, failureThreshold: 1, enabled: true);
        var changes = _service.DetectChanges(
            oldAlert,
            "New Alert",
            false,
            false,
            5,
            false,
            false,
            [Guid.NewGuid()]);

        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Name"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Trigger on Degraded"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Trigger on Down"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Failure Threshold"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Send Recovery Notification"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Enabled"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Notification Channels"));
        Assert.IsTrue(changes.ContainsKey("Alert 'Old Alert' (renamed to 'New Alert'): Updated At"));
        Assert.HasCount(8, changes);
    }

    #endregion

    #region BuildCreationInfo Tests

    [TestMethod]
    public void BuildCreationInfoShouldIncludeAlertName()
    {
        var info = _service.BuildCreationInfo("Test Alert", true, true, 1, true, true, []);

        Assert.IsTrue(info.ContainsKey("Alert Created"));
        Assert.AreEqual("Test Alert", info["Alert Created"]);
    }

    [TestMethod]
    public void BuildCreationInfoShouldIncludeTriggerOnWarnOnly()
    {
        var info = _service.BuildCreationInfo("Test Alert", true, false, 1, true, true, []);

        Assert.IsTrue(info.ContainsKey("Triggers"));
        Assert.AreEqual("Degraded", info["Triggers"]);
    }

    [TestMethod]
    public void BuildCreationInfoShouldIncludeTriggerOnDownOnly()
    {
        var info = _service.BuildCreationInfo("Test Alert", false, true, 1, true, true, []);

        Assert.IsTrue(info.ContainsKey("Triggers"));
        Assert.AreEqual("Down", info["Triggers"]);
    }

    [TestMethod]
    public void BuildCreationInfoShouldIncludeBothTriggers()
    {
        var info = _service.BuildCreationInfo("Test Alert", true, true, 1, true, true, []);

        Assert.IsTrue(info.ContainsKey("Triggers"));
        Assert.AreEqual("Degraded, Down", info["Triggers"]);
    }

    [TestMethod]
    public void BuildCreationInfoShouldNotIncludeTriggersWhenNeitherSet()
    {
        var info = _service.BuildCreationInfo("Test Alert", false, false, 1, true, true, []);

        Assert.IsFalse(info.ContainsKey("Triggers"));
    }

    [TestMethod]
    public void BuildCreationInfoShouldIncludeFailureThreshold()
    {
        var info = _service.BuildCreationInfo("Test Alert", true, true, 5, true, true, []);

        Assert.IsTrue(info.ContainsKey("Failure Threshold"));
        Assert.AreEqual("5", info["Failure Threshold"]);
    }

    [TestMethod]
    public void BuildCreationInfoShouldIncludeRecoveryNotificationsWhenEnabled()
    {
        var info = _service.BuildCreationInfo("Test Alert", true, true, 1, true, true, []);

        Assert.IsTrue(info.ContainsKey("Recovery Notifications"));
        Assert.AreEqual("enabled", info["Recovery Notifications"]);
    }

    [TestMethod]
    public void BuildCreationInfoShouldNotIncludeRecoveryNotificationsWhenDisabled()
    {
        var info = _service.BuildCreationInfo("Test Alert", true, true, 1, false, true, []);

        Assert.IsFalse(info.ContainsKey("Recovery Notifications"));
    }

    [TestMethod]
    public void BuildCreationInfoShouldIncludeAlertStatusWhenDisabled()
    {
        var info = _service.BuildCreationInfo("Test Alert", true, true, 1, true, false, []);

        Assert.IsTrue(info.ContainsKey("Alert Status"));
        Assert.AreEqual("disabled", info["Alert Status"]);
    }

    [TestMethod]
    public void BuildCreationInfoShouldNotIncludeAlertStatusWhenEnabled()
    {
        var info = _service.BuildCreationInfo("Test Alert", true, true, 1, true, true, []);

        Assert.IsFalse(info.ContainsKey("Alert Status"));
    }

    [TestMethod]
    public void BuildCreationInfoShouldShowSelectedChannelCount()
    {
        var channelIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var info = _service.BuildCreationInfo("Test Alert", true, true, 1, true, true, channelIds);

        Assert.IsTrue(info.ContainsKey("Notification Channels"));
        Assert.AreEqual("3 selected", info["Notification Channels"]);
    }

    [TestMethod]
    public void BuildCreationInfoShouldShowAllWorkspaceChannelsWhenNoneSelected()
    {
        var info = _service.BuildCreationInfo("Test Alert", true, true, 1, true, true, []);

        Assert.IsTrue(info.ContainsKey("Notification Channels"));
        Assert.AreEqual("all workspace channels", info["Notification Channels"]);
    }

    [TestMethod]
    public void BuildCreationInfoShouldIncludeAllFieldsForCompleteAlert()
    {
        var channelIds = new List<Guid> { Guid.NewGuid() };
        var info = _service.BuildCreationInfo("Complete Alert", true, true, 3, true, true, channelIds);

        Assert.IsTrue(info.ContainsKey("Alert Created"));
        Assert.IsTrue(info.ContainsKey("Triggers"));
        Assert.IsTrue(info.ContainsKey("Failure Threshold"));
        Assert.IsTrue(info.ContainsKey("Recovery Notifications"));
        Assert.IsTrue(info.ContainsKey("Notification Channels"));
        Assert.IsFalse(info.ContainsKey("Alert Status"));
    }

    [TestMethod]
    public void BuildCreationInfoShouldIncludeMinimalFieldsForSimpleAlert()
    {
        var info = _service.BuildCreationInfo("Simple Alert", false, false, 1, false, true, []);

        Assert.IsTrue(info.ContainsKey("Alert Created"));
        Assert.IsTrue(info.ContainsKey("Failure Threshold"));
        Assert.IsTrue(info.ContainsKey("Notification Channels"));
        Assert.IsFalse(info.ContainsKey("Triggers"));
        Assert.IsFalse(info.ContainsKey("Recovery Notifications"));
        Assert.IsFalse(info.ContainsKey("Alert Status"));
    }

    #endregion

    #region BuildDeletionInfo Tests

    [TestMethod]
    public void BuildDeletionInfoShouldIncludeAlertName()
    {
        var info = _service.BuildDeletionInfo("Deleted Alert");

        Assert.IsTrue(info.ContainsKey("Alert Deleted"));
        Assert.AreEqual("Deleted Alert", info["Alert Deleted"]);
        Assert.HasCount(1, info);
    }

    [TestMethod]
    public void BuildDeletionInfoShouldOnlyIncludeAlertDeleted()
    {
        var info = _service.BuildDeletionInfo("Test");

        Assert.HasCount(1, info);
        Assert.IsTrue(info.ContainsKey("Alert Deleted"));
    }

    #endregion

    private Alert CreateAlert(
        string name,
        bool triggerOnWarn = true,
        bool triggerOnDown = true,
        int failureThreshold = 1,
        bool sendRecoveryNotification = true,
        bool enabled = true,
        List<NotificationChannel>? channels = null)
    {
        return new Alert
        {
            Id = Guid.NewGuid(),
            CheckId = Guid.NewGuid(),
            Name = name,
            TriggerOnWarn = triggerOnWarn,
            TriggerOnDown = triggerOnDown,
            FailureThreshold = failureThreshold,
            SendRecoveryNotification = sendRecoveryNotification,
            Enabled = enabled,
            NotificationChannels = channels ?? [],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
