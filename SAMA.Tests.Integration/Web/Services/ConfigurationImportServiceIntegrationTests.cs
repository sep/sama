using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Constants;
using SAMA.Web.Models.Export;
using SAMA.Web.Services;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class ConfigurationImportServiceIntegrationTests : IntegrationTestBase
{
    private ConfigurationImportService _importService = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();
        _importService = new ConfigurationImportService(DbContext);
    }

    [TestMethod]
    public async Task ImportAsyncShouldCreateWorkspaceFromExport()
    {
        var export = CreateBasicExport();

        var result = await _importService.ImportAsync(export);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.WorkspacesCreated);
        Assert.IsEmpty(result.Errors);

        var workspace = await DbContext.Workspaces.FirstOrDefaultAsync(w => w.Name == "Test Workspace");
        Assert.IsNotNull(workspace);
        Assert.AreEqual("Test Description", workspace.Description);
        Assert.IsTrue(workspace.IsPublic);
    }

    [TestMethod]
    public async Task ImportAsyncShouldCreateChecksWithConfiguration()
    {
        var export = CreateBasicExport();
        export.Workspaces[0].Checks.Add(new CheckExportDto
        {
            Name = "API Health",
            Description = "Health check",
            CheckType = CheckTypes.Http,
            Configuration = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://example.com/health")
            },
            IntervalSeconds = 60,
            TimeoutSeconds = 30,
            Enabled = true
        });

        var result = await _importService.ImportAsync(export);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.ChecksCreated);

        var check = await DbContext.Checks.FirstOrDefaultAsync(c => c.Name == "API Health");
        Assert.IsNotNull(check);
        Assert.AreEqual(CheckTypes.Http, check.CheckType);
        Assert.AreEqual(60, check.IntervalSeconds);
    }

    [TestMethod]
    public async Task ImportAsyncShouldCreateNotificationChannels()
    {
        var export = CreateBasicExport();
        export.Workspaces[0].NotificationChannels.Add(new NotificationChannelExportDto
        {
            ExportId = "channel_1",
            Name = "Slack Alerts",
            ChannelType = ChannelTypes.Slack,
            Configuration = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Webhook.WebhookUrl] = JsonSerializer.SerializeToElement("https://hooks.slack.com/test")
            },
            Enabled = true
        });

        var result = await _importService.ImportAsync(export);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.NotificationChannelsCreated);

        var channel = await DbContext.NotificationChannels.FirstOrDefaultAsync(c => c.Name == "Slack Alerts");
        Assert.IsNotNull(channel);
        Assert.AreEqual(ChannelTypes.Slack, channel.ChannelType);
    }

    [TestMethod]
    public async Task ImportAsyncShouldCreateAlertsWithChannelReferences()
    {
        var export = CreateBasicExport();
        export.Workspaces[0].NotificationChannels.Add(new NotificationChannelExportDto
        {
            ExportId = "channel_1",
            Name = "Slack Alerts",
            ChannelType = ChannelTypes.Slack,
            Configuration = new Dictionary<string, JsonElement>(),
            Enabled = true
        });
        export.Workspaces[0].Checks.Add(new CheckExportDto
        {
            Name = "API Health",
            CheckType = CheckTypes.Http,
            Configuration = new Dictionary<string, JsonElement>(),
            IntervalSeconds = 60,
            TimeoutSeconds = 30,
            Enabled = true,
            Alerts =
            [
                new AlertExportDto
                {
                    Name = "Critical Alert",
                    TriggerOnDown = true,
                    FailureThreshold = 3,
                    SendRecoveryNotification = true,
                    Enabled = true,
                    NotificationChannelExportIds = ["channel_1"]
                }
            ]
        });

        var result = await _importService.ImportAsync(export);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.AlertsCreated);

        var alert = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstOrDefaultAsync(a => a.Name == "Critical Alert");
        Assert.IsNotNull(alert);
        Assert.AreEqual(3, alert.FailureThreshold);
        Assert.HasCount(1, alert.NotificationChannels);
        Assert.AreEqual("Slack Alerts", alert.NotificationChannels.First().Name);
    }

    [TestMethod]
    public async Task ImportAsyncShouldSkipExistingWorkspaceByDefault()
    {
        var existingWorkspace = new Workspace
        {
            Name = "Test Workspace",
            Description = "Original",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Workspaces.Add(existingWorkspace);
        await DbContext.SaveChangesAsync();

        var export = CreateBasicExport();
        export.Workspaces[0].Description = "Updated";

        var result = await _importService.ImportAsync(export);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.WorkspacesCreated);
        Assert.HasCount(1, result.Warnings);

        var workspace = await DbContext.Workspaces.FirstOrDefaultAsync(w => w.Name == "Test Workspace");
        Assert.IsNotNull(workspace);
        Assert.AreEqual("Original", workspace.Description);
    }

    [TestMethod]
    public async Task ImportAsyncShouldMergeIntoExistingWorkspace()
    {
        var existingWorkspace = new Workspace
        {
            Name = "Test Workspace",
            Description = "Original",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Workspaces.Add(existingWorkspace);
        await DbContext.SaveChangesAsync();

        var existingChannel = new NotificationChannel
        {
            WorkspaceId = existingWorkspace.Id,
            Name = "Existing Channel",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>(),
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.NotificationChannels.Add(existingChannel);
        await DbContext.SaveChangesAsync();

        var export = CreateBasicExport();
        export.Workspaces[0].Description = "Updated";
        export.Workspaces[0].NotificationChannels.Add(new NotificationChannelExportDto
        {
            ExportId = "channel_1",
            Name = "New Channel",
            ChannelType = ChannelTypes.Slack,
            Configuration = new Dictionary<string, JsonElement>(),
            Enabled = true
        });

        var result = await _importService.ImportAsync(export, ImportMergeStrategy.MergeIntoExisting);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.WorkspacesUpdated);
        Assert.AreEqual(1, result.NotificationChannelsCreated);

        var workspace = await DbContext.Workspaces.FirstOrDefaultAsync(w => w.Name == "Test Workspace");
        Assert.IsNotNull(workspace);
        Assert.AreEqual("Updated", workspace.Description);

        var channels = await DbContext.NotificationChannels.Where(c => c.WorkspaceId == workspace.Id).ToListAsync();
        Assert.HasCount(2, channels);
    }

    [TestMethod]
    public async Task ImportAsyncShouldReplaceExistingWorkspace()
    {
        var existingWorkspace = new Workspace
        {
            Name = "Test Workspace",
            Description = "Original",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Workspaces.Add(existingWorkspace);
        await DbContext.SaveChangesAsync();

        var existingChannel = new NotificationChannel
        {
            WorkspaceId = existingWorkspace.Id,
            Name = "Old Channel",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>(),
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.NotificationChannels.Add(existingChannel);
        await DbContext.SaveChangesAsync();

        var export = CreateBasicExport();
        export.Workspaces[0].Description = "Replaced";
        export.Workspaces[0].NotificationChannels.Add(new NotificationChannelExportDto
        {
            ExportId = "channel_1",
            Name = "New Channel",
            ChannelType = ChannelTypes.Slack,
            Configuration = new Dictionary<string, JsonElement>(),
            Enabled = true
        });

        var result = await _importService.ImportAsync(export, ImportMergeStrategy.ReplaceExisting);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.WorkspacesCreated);

        var workspace = await DbContext.Workspaces.FirstOrDefaultAsync(w => w.Name == "Test Workspace");
        Assert.IsNotNull(workspace);
        Assert.AreEqual("Replaced", workspace.Description);

        var channels = await DbContext.NotificationChannels.Where(c => c.WorkspaceId == workspace.Id).ToListAsync();
        Assert.HasCount(1, channels);
        Assert.AreEqual("New Channel", channels[0].Name);
    }

    [TestMethod]
    public async Task ImportAsyncShouldRejectFutureSchemaVersion()
    {
        var export = CreateBasicExport();
        export.SchemaVersion = 999;

        var result = await _importService.ImportAsync(export);

        Assert.IsFalse(result.Success);
        Assert.HasCount(1, result.Errors);
        Assert.Contains("newer than supported", result.Errors[0]);
    }

    [TestMethod]
    public async Task ImportAsyncShouldWarnOnUnknownChannelReference()
    {
        var export = CreateBasicExport();
        export.Workspaces[0].Checks.Add(new CheckExportDto
        {
            Name = "API Health",
            CheckType = CheckTypes.Http,
            Configuration = new Dictionary<string, JsonElement>(),
            IntervalSeconds = 60,
            TimeoutSeconds = 30,
            Enabled = true,
            Alerts =
            [
                new AlertExportDto
                {
                    Name = "Critical Alert",
                    TriggerOnDown = true,
                    FailureThreshold = 3,
                    Enabled = true,
                    NotificationChannelExportIds = ["nonexistent_channel"]
                }
            ]
        });

        var result = await _importService.ImportAsync(export);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.AlertsCreated);
        Assert.IsTrue(result.Warnings.Any(w => w.Contains("nonexistent_channel")));

        var alert = await DbContext.Alerts
            .Include(a => a.NotificationChannels)
            .FirstOrDefaultAsync(a => a.Name == "Critical Alert");
        Assert.IsNotNull(alert);
        Assert.IsEmpty(alert.NotificationChannels);
    }

    [TestMethod]
    public async Task ImportAsyncShouldImportEventSubscriptions()
    {
        var export = CreateBasicExport();
        export.Workspaces[0].NotificationChannels.Add(new NotificationChannelExportDto
        {
            ExportId = "channel_1",
            Name = "Slack Channel",
            ChannelType = ChannelTypes.Slack,
            Configuration = new Dictionary<string, JsonElement>(),
            Enabled = true,
            EventSubscriptions =
            [
                "CheckCreated",
                "CheckStatusChanged"
            ]
        });

        var result = await _importService.ImportAsync(export);

        Assert.IsTrue(result.Success);

        var channel = await DbContext.NotificationChannels
            .Include(c => c.EventSubscriptions)
            .FirstOrDefaultAsync(c => c.Name == "Slack Channel");
        Assert.IsNotNull(channel);
        Assert.HasCount(2, channel.EventSubscriptions);
    }

    private static SamaExportDto CreateBasicExport()
    {
        return new SamaExportDto
        {
            SchemaVersion = 1,
            ExportedFromVersion = "1.0.0",
            ExportedAt = DateTimeOffset.UtcNow,
            Workspaces =
            [
                new WorkspaceExportDto
                {
                    Name = "Test Workspace",
                    Description = "Test Description",
                    IsPublic = true
                }
            ]
        };
    }
}
