using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SAMA.Data.Entities;
using SAMA.Data.Services;
using SAMA.Shared.Constants;
using SAMA.Web.Constants;
using SAMA.Web.Models.Export;
using SAMA.Web.Services;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class ConfigurationExportServiceIntegrationTests : IntegrationTestBase
{
    private const string TestPassword = "test-export-password-123";
    private ConfigurationExportService _exportService = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();
        var appStateService = new ApplicationStateService();
        var encryptionService = new AesEncryptionService();
        _exportService = new ConfigurationExportService(DbContext, appStateService, encryptionService);
    }

    [TestMethod]
    public async Task ExportAllAsyncShouldReturnEmptyExportWhenNoData()
    {
        var result = await _exportService.ExportAllAsync(TestPassword);

        Assert.AreEqual(1, result.SchemaVersion);
        Assert.IsNotNull(result.ExportedFromVersion);
        Assert.IsTrue(result.ExportedAt <= DateTimeOffset.UtcNow);
        Assert.IsFalse(string.IsNullOrEmpty(result.EncryptedData));

        var payload = DecryptPayload(result.EncryptedData);
        Assert.IsEmpty(payload);
    }

    [TestMethod]
    public async Task ExportAllAsyncShouldExportWorkspaceWithChecksAndChannels()
    {
        var workspace = new Workspace
        {
            Name = "Test Workspace",
            Description = "Test Description",
            IsPublic = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();

        var channel = new NotificationChannel
        {
            WorkspaceId = workspace.Id,
            Name = "Test Channel",
            ChannelType = ChannelTypes.Slack,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Webhook.WebhookUrl] = JsonSerializer.SerializeToElement("https://hooks.slack.com/test")
            },
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.NotificationChannels.Add(channel);

        var check = new Check
        {
            WorkspaceId = workspace.Id,
            Name = "Test Check",
            Description = "Check Description",
            CheckType = CheckTypes.Http,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com")
            },
            IntervalSeconds = 60,
            TimeoutSeconds = 30,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Checks.Add(check);
        await DbContext.SaveChangesAsync();

        var alert = new Alert
        {
            CheckId = check.Id,
            Name = "Test Alert",
            TriggerOnWarn = false,
            TriggerOnDown = true,
            FailureThreshold = 3,
            SendRecoveryNotification = true,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        alert.NotificationChannels.Add(channel);
        DbContext.Alerts.Add(alert);
        await DbContext.SaveChangesAsync();

        var result = await _exportService.ExportAllAsync(TestPassword);
        var payload = DecryptPayload(result.EncryptedData);

        Assert.HasCount(1, payload);
        var exportedWorkspace = payload[0];
        Assert.AreEqual("Test Workspace", exportedWorkspace.Name);
        Assert.AreEqual("Test Description", exportedWorkspace.Description);
        Assert.IsTrue(exportedWorkspace.IsPublic);

        Assert.HasCount(1, exportedWorkspace.NotificationChannels);
        var exportedChannel = exportedWorkspace.NotificationChannels[0];
        Assert.AreEqual("Test Channel", exportedChannel.Name);
        Assert.AreEqual(ChannelTypes.Slack, exportedChannel.ChannelType);
        Assert.IsNotNull(exportedChannel.ExportId);

        Assert.HasCount(1, exportedWorkspace.Checks);
        var exportedCheck = exportedWorkspace.Checks[0];
        Assert.AreEqual("Test Check", exportedCheck.Name);
        Assert.AreEqual(CheckTypes.Http, exportedCheck.CheckType);
        Assert.AreEqual(60, exportedCheck.IntervalSeconds);

        Assert.HasCount(1, exportedCheck.Alerts);
        var exportedAlert = exportedCheck.Alerts[0];
        Assert.AreEqual("Test Alert", exportedAlert.Name);
        Assert.AreEqual(3, exportedAlert.FailureThreshold);
        Assert.HasCount(1, exportedAlert.NotificationChannelExportIds);
        Assert.AreEqual(exportedChannel.ExportId, exportedAlert.NotificationChannelExportIds[0]);
    }

    [TestMethod]
    public async Task ExportAllAsyncShouldExportEventSubscriptions()
    {
        var workspace = new Workspace
        {
            Name = "Test Workspace",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();

        var channel = new NotificationChannel
        {
            WorkspaceId = workspace.Id,
            Name = "Test Channel",
            ChannelType = ChannelTypes.Slack,
            ConfigurationJson = new Dictionary<string, JsonElement>(),
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.NotificationChannels.Add(channel);
        await DbContext.SaveChangesAsync();

        var subscription = new EventSubscription
        {
            NotificationChannelId = channel.Id,
            EventType = "CheckCreated",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.EventSubscriptions.Add(subscription);
        await DbContext.SaveChangesAsync();

        var result = await _exportService.ExportAllAsync(TestPassword);
        var payload = DecryptPayload(result.EncryptedData);

        Assert.HasCount(1, payload[0].NotificationChannels[0].EventSubscriptions);
        Assert.AreEqual("CheckCreated", payload[0].NotificationChannels[0].EventSubscriptions[0]);
    }

    private static List<WorkspaceExportDto> DecryptPayload(string encryptedData)
    {
        var encryptionService = new AesEncryptionService();
        var decryptedJson = encryptionService.Decrypt(encryptedData, TestPassword);
        return JsonSerializer.Deserialize<List<WorkspaceExportDto>>(decryptedJson)!;
    }
}
