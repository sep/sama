using SAMA.Data.Entities;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Integration.Web.Services.Queries;

[TestClass]
public class ChannelQueryServiceTests : IntegrationTestBase
{
    private ChannelQueryService _service = null!;
    private Workspace _workspace = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _workspace = await CreateWorkspaceAsync("Test Workspace");

        var maskingService = new SensitiveDataMaskingService();
        _service = new ChannelQueryService(DbContext, maskingService);
    }

    [TestMethod]
    public async Task GetChannelsForWorkspaceAsyncShouldReturnEmptyListWhenNoChannels()
    {
        var result = await _service.GetChannelsForWorkspaceAsync(_workspace.Id);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetChannelsForWorkspaceAsyncShouldReturnChannelsOrderedByName()
    {
        await CreateNotificationChannelAsync(_workspace.Id, "Zebra", "Email", true);
        await CreateNotificationChannelAsync(_workspace.Id, "Alpha", "Slack", false);

        var result = await _service.GetChannelsForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(2, result);
        Assert.AreEqual("Alpha", result[0].Name);
        Assert.AreEqual("Zebra", result[1].Name);
    }

    [TestMethod]
    public async Task GetChannelsForWorkspaceAsyncShouldOnlyReturnChannelsForSpecifiedWorkspace()
    {
        var otherWorkspace = await CreateWorkspaceAsync("Other Workspace");
        await CreateNotificationChannelAsync(_workspace.Id, "Workspace 1 Channel", "Email", true);
        await CreateNotificationChannelAsync(otherWorkspace.Id, "Other Workspace Channel", "Slack", true);

        var result = await _service.GetChannelsForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.AreEqual("Workspace 1 Channel", result[0].Name);
    }

    [TestMethod]
    public async Task GetChannelDetailsAsyncShouldReturnNullWhenChannelDoesNotExist()
    {
        var result = await _service.GetChannelDetailsAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetChannelDetailsAsyncShouldReturnChannelWithAllProperties()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Test Channel", "Email", true);

        var result = await _service.GetChannelDetailsAsync(channel.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(channel.Id, result.Id);
        Assert.AreEqual(_workspace.Id, result.WorkspaceId);
        Assert.AreEqual("Test Workspace", result.WorkspaceName);
        Assert.AreEqual("Test Channel", result.Name);
        Assert.AreEqual("Email", result.ChannelType);
        Assert.IsTrue(result.Enabled);
        Assert.IsNotNull(result.MaskedConfiguration);
        Assert.IsNotNull(result.ConfigurationJson);
    }

    [TestMethod]
    public async Task GetChannelDetailsAsyncShouldIncludeCounts()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var check = await CreateCheckAsync("Check", "Http", "60", true);
        var alert = await CreateAlertAsync(check.Id, "Alert", true, true, 1, true, true);

        await AttachChannelToAlertAsync(alert.Id, channel.Id);

        var result = await _service.GetChannelDetailsAsync(channel.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.AlertCount);
    }

    [TestMethod]
    public async Task GetChannelDetailsAsyncShouldIncludeAlertsWithNoChannels()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var check = await CreateCheckAsync("Check", "Http", "60", true);
        var alertWithNoChannels = await CreateAlertAsync(check.Id, "Alert Without Channels", true, true, 1, true, true);

        var result = await _service.GetChannelDetailsAsync(channel.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.AlertCount, "Should count alert with no specific channels");
    }

    [TestMethod]
    public async Task GetChannelDetailsAsyncShouldCountBothExplicitAndAllChannelAlerts()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var check1 = await CreateCheckAsync("Check 1", "Http", "60", true);
        var check2 = await CreateCheckAsync("Check 2", "Http", "60", true);

        var alertWithChannel = await CreateAlertAsync(check1.Id, "Alert With Channel", true, true, 1, true, true);
        await AttachChannelToAlertAsync(alertWithChannel.Id, channel.Id);

        var alertWithoutChannel = await CreateAlertAsync(check2.Id, "Alert Without Channel", true, true, 1, true, true);

        var result = await _service.GetChannelDetailsAsync(channel.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.AlertCount, "Should count both explicit and all-channels alerts");
    }

    [TestMethod]
    public async Task GetChannelDetailsAsyncShouldOnlyCountAlertsFromSameWorkspace()
    {
        var channel = await CreateNotificationChannelAsync(_workspace.Id, "Channel", "Email", true);
        var otherWorkspace = await CreateWorkspaceAsync("Other Workspace");

        var check1 = await CreateCheckAsync("Check 1", "Http", "60", true);
        var alert1 = await CreateAlertAsync(check1.Id, "Alert 1", true, true, 1, true, true);

        var check2 = new Check
        {
            WorkspaceId = otherWorkspace.Id,
            Name = "Check 2",
            CheckType = "Http",
            ConfigurationJson = [],
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Checks.Add(check2);
        await DbContext.SaveChangesAsync();

        var alert2 = await CreateAlertAsync(check2.Id, "Alert 2", true, true, 1, true, true);

        DbContext.ChangeTracker.Clear();

        var result = await _service.GetChannelDetailsAsync(channel.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.AlertCount, "Should only count alerts from the same workspace");
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
