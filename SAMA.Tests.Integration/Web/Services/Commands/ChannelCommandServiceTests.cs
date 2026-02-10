using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Web.Services.Commands;

namespace SAMA.Tests.Integration.Web.Services.Commands;

[TestClass]
public class ChannelCommandServiceTests : IntegrationTestBase
{
    private ChannelCommandService _service = null!;
    private Workspace _workspace = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _workspace = await CreateWorkspaceAsync("Test Workspace");
        _service = new ChannelCommandService(DbContext, Substitute.For<ILogger<ChannelCommandService>>());
    }

    [TestMethod]
    public async Task CreateChannelAsyncShouldCreateChannelWithBasicProperties()
    {
        var config = new Dictionary<string, JsonElement>
        {
            ["SmtpHost"] = JsonSerializer.SerializeToElement("smtp.example.com"),
            ["SmtpPort"] = JsonSerializer.SerializeToElement(587)
        };

        var channelId = await _service.CreateChannelAsync(
            _workspace.Id,
            "Test Email Channel",
            "Email",
            config,
            true,
            "admin");

        Assert.AreNotEqual(Guid.Empty, channelId);

        var channel = await DbContext.NotificationChannels.FindAsync(channelId);
        Assert.IsNotNull(channel);
        Assert.AreEqual(_workspace.Id, channel.WorkspaceId);
        Assert.AreEqual("Test Email Channel", channel.Name);
        Assert.AreEqual("Email", channel.ChannelType);
        Assert.IsTrue(channel.Enabled);
        Assert.HasCount(2, channel.ConfigurationJson);
    }

    [TestMethod]
    public async Task CreateChannelAsyncShouldCreateDisabledChannel()
    {
        var channelId = await _service.CreateChannelAsync(
            _workspace.Id,
            "Disabled Channel",
            "Slack",
            [],
            false,
            "admin");

        var channel = await DbContext.NotificationChannels.FindAsync(channelId);
        Assert.IsNotNull(channel);
        Assert.IsFalse(channel.Enabled);
    }

    [TestMethod]
    public async Task UpdateChannelAsyncShouldReturnFalseWhenChannelDoesNotExist()
    {
        var result = await _service.UpdateChannelAsync(
            Guid.NewGuid(),
            "Updated",
            "Email",
            [],
            true,
            "admin");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateChannelAsyncShouldUpdateAllProperties()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Original", "Email", true);
        var originalUpdatedAt = channel.UpdatedAt;

        var newConfig = new Dictionary<string, JsonElement>
        {
            ["WebhookUrl"] = JsonSerializer.SerializeToElement("https://hooks.slack.com/test")
        };

        var result = await _service.UpdateChannelAsync(
            channel.Id,
            "Updated Channel",
            "Slack",
            newConfig,
            false,
            "admin");

        Assert.IsTrue(result);

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.NotificationChannels.FindAsync(channel.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual("Updated Channel", updated.Name);
        Assert.AreEqual("Slack", updated.ChannelType);
        Assert.IsFalse(updated.Enabled);
        Assert.HasCount(1, updated.ConfigurationJson);
        Assert.IsTrue(updated.UpdatedAt > originalUpdatedAt);
    }

    [TestMethod]
    public async Task DeleteChannelAsyncShouldReturnFalseWhenChannelDoesNotExist()
    {
        var result = await _service.DeleteChannelAsync(Guid.NewGuid(), "admin");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DeleteChannelAsyncShouldDeleteChannel()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "To Delete", "Email", true);

        var result = await _service.DeleteChannelAsync(channel.Id, "admin");

        Assert.IsTrue(result);

        var deleted = await DbContext.NotificationChannels.FindAsync(channel.Id);
        Assert.IsNull(deleted);
    }

    [TestMethod]
    public async Task DeleteChannelAsyncShouldRemoveChannelFromAlerts()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var check = await CreateCheckAsync("Check", "Http", "60", true);
        var alert = await CreateAlertAsync(check.Id, "Alert", true, true, 1, true, true);

        await AttachChannelToAlertAsync(alert.Id, channel.Id);

        await _service.DeleteChannelAsync(channel.Id, "admin");

        DbContext.ChangeTracker.Clear();
        var updatedAlert = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstAsync(a => a.Id == alert.Id);

        Assert.IsEmpty(updatedAlert.NotificationChannels);
    }

    [TestMethod]
    public async Task DeleteChannelAsyncShouldDeleteRelatedAlertHistory()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var check = await CreateCheckAsync("Check", "Http", "60", true);
        var alert = await CreateAlertAsync(check.Id, "Alert", true, true, 1, true, true);

        var history = new AlertHistory
        {
            AlertId = alert.Id,
            NotificationChannelId = channel.Id,
            TriggerEventId = Guid.NewGuid(),
            Status = "Down",
            Message = "Test",
            SentAt = DateTimeOffset.UtcNow,
            Success = true
        };
        DbContext.AlertHistories.Add(history);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await _service.DeleteChannelAsync(channel.Id, "admin");

        var deletedHistory = await DbContext.AlertHistories.FindAsync(history.Id);
        Assert.IsNull(deletedHistory);
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

    private async Task<NotificationChannel> CreateNotificationChannelAsync(Guid workspaceId, string name, string channelType, bool enabled)
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

    private async Task<Alert> CreateAlertAsync(Guid checkId, string name, bool triggerOnWarn, bool triggerOnDown, int failureThreshold, bool sendRecoveryNotification, bool enabled)
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

    private async Task AttachChannelToAlertAsync(Guid alertId, Guid channelId)
    {
        var alert = await DbContext.Alerts.FindAsync(alertId);
        var channel = await DbContext.NotificationChannels.FindAsync(channelId);

        if (alert != null && channel != null)
        {
            alert.NotificationChannels.Add(channel);
            await DbContext.SaveChangesAsync();
            DbContext.ChangeTracker.Clear();
        }
    }
}
