using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Data.Services;
using SAMA.Shared.Constants;
using SAMA.Web.Constants;
using SAMA.Web.Models.Export;
using SAMA.Web.Services;

namespace SAMA.Tests.Integration.Web.Services;

[TestClass]
public class ConfigurationImportServiceIntegrationTests : IntegrationTestBase
{
    private const string TestPassword = "test-export-password-123";
    private ConfigurationImportService _importService = null!;
    private CheckSchedulerService _mockScheduler = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();
        var encryptionService = new AesEncryptionService();
        _mockScheduler = Substitute.For<CheckSchedulerService>(null, null, null);
        _importService = new ConfigurationImportService(DbContext, encryptionService, _mockScheduler);
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

        var export = CreateEncryptedExport([new WorkspaceExportDto { Name = "Test Workspace", Description = "Updated", IsPublic = true }]);

        var result = await _importService.ImportAsync(export, TestPassword);

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

        var export = CreateEncryptedExport(
        [
            new WorkspaceExportDto
            {
                Name = "Test Workspace",
                Description = "Updated",
                IsPublic = true,
                NotificationChannels =
                [
                    new NotificationChannelExportDto
                    {
                        ExportId = "channel_1",
                        Name = "New Channel",
                        ChannelType = ChannelTypes.Slack,
                        Configuration = new Dictionary<string, JsonElement>(),
                        Enabled = true
                    }
                ]
            }
        ]);

        var result = await _importService.ImportAsync(export, TestPassword, ImportMergeStrategy.MergeIntoExisting);

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
    public async Task ImportAsyncShouldSkipExistingNotificationChannelOnMerge()
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

        var export = CreateEncryptedExport(
        [
            new WorkspaceExportDto
            {
                Name = "Test Workspace",
                Description = "Updated",
                IsPublic = true,
                NotificationChannels =
                [
                    new NotificationChannelExportDto
                    {
                        ExportId = "channel_1",
                        Name = "Existing Channel",
                        ChannelType = ChannelTypes.Slack,
                        Configuration = new Dictionary<string, JsonElement>(),
                        Enabled = false
                    }
                ]
            }
        ]);

        var result = await _importService.ImportAsync(export, TestPassword, ImportMergeStrategy.MergeIntoExisting);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.WorkspacesUpdated);
        Assert.AreEqual(0, result.NotificationChannelsCreated);
        Assert.HasCount(1, result.Warnings);
        Assert.IsTrue(result.Warnings.Any(w => w.Contains("Existing Channel") && w.Contains("already exists")));

        var workspace = await DbContext.Workspaces.FirstOrDefaultAsync(w => w.Name == "Test Workspace");
        Assert.IsNotNull(workspace);

        var channels = await DbContext.NotificationChannels.Where(c => c.WorkspaceId == workspace.Id).ToListAsync();
        Assert.HasCount(1, channels);
        Assert.AreEqual("Existing Channel", channels[0].Name);
        Assert.AreEqual(ChannelTypes.Email, channels[0].ChannelType);
        Assert.IsTrue(channels[0].Enabled);
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

        var export = CreateEncryptedExport(
        [
            new WorkspaceExportDto
            {
                Name = "Test Workspace",
                Description = "Replaced",
                IsPublic = true,
                NotificationChannels =
                [
                    new NotificationChannelExportDto
                    {
                        ExportId = "channel_1",
                        Name = "New Channel",
                        ChannelType = ChannelTypes.Slack,
                        Configuration = new Dictionary<string, JsonElement>(),
                        Enabled = true
                    }
                ]
            }
        ]);

        var result = await _importService.ImportAsync(export, TestPassword, ImportMergeStrategy.ReplaceExisting);

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
        var export = CreateEncryptedExport([new WorkspaceExportDto { Name = "Test Workspace", IsPublic = true }]);
        export.SchemaVersion = 999;

        var result = await _importService.ImportAsync(export, TestPassword);

        Assert.IsFalse(result.Success);
        Assert.HasCount(1, result.Errors);
        Assert.Contains("newer than supported", result.Errors[0]);
    }

    [TestMethod]
    public async Task ImportAsyncShouldMigrateV1ExportWithIntervalSeconds()
    {
        var v1Workspaces = new[]
        {
            new
            {
                Name = "V1 Workspace",
                Description = "Exported from v1",
                IsPublic = true,
                NotificationChannels = Array.Empty<object>(),
                Checks = new[]
                {
                    new
                    {
                        Name = "HTTP Check",
                        CheckType = CheckTypes.Http,
                        Configuration = new Dictionary<string, JsonElement>(),
                        IntervalSeconds = 60,
                        TimeoutSeconds = 30,
                        Enabled = true,
                        Alerts = Array.Empty<object>()
                    }
                }
            }
        };

        var payloadJson = JsonSerializer.Serialize(v1Workspaces);
        var encryptionService = new AesEncryptionService();
        var encryptedData = encryptionService.Encrypt(payloadJson, TestPassword);

        var export = new SamaExportDto
        {
            SchemaVersion = 1,
            ExportedFromVersion = "1.0.0",
            ExportedAt = DateTimeOffset.UtcNow,
            EncryptedData = encryptedData
        };

        var result = await _importService.ImportAsync(export, TestPassword);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.WorkspacesCreated);
        Assert.AreEqual(1, result.ChecksCreated);
        Assert.IsTrue(result.Warnings.Any(w => w.Contains("v1 to v2")));

        var check = await DbContext.Checks.FirstOrDefaultAsync(c => c.Name == "HTTP Check");
        Assert.IsNotNull(check);
        Assert.AreEqual("60", check.Schedule);
    }

    [TestMethod]
    public async Task ImportAsyncShouldFailWithWrongPassword()
    {
        var export = CreateEncryptedExport([new WorkspaceExportDto { Name = "Test Workspace", IsPublic = true }]);

        var result = await _importService.ImportAsync(export, "wrong-password");

        Assert.IsFalse(result.Success);
        Assert.HasCount(1, result.Errors);
        Assert.Contains("password may be incorrect", result.Errors[0]);
    }

    [TestMethod]
    public async Task ImportAsyncShouldWarnOnUnknownChannelReference()
    {
        var export = CreateEncryptedExport(
        [
            new WorkspaceExportDto
            {
                Name = "Test Workspace",
                IsPublic = true,
                Checks =
                [
                    new CheckExportDto
                    {
                        Name = "API Health",
                        CheckType = CheckTypes.Http,
                        Configuration = new Dictionary<string, JsonElement>(),
                        Schedule = "60",
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
                    }
                ]
            }
        ]);

        var result = await _importService.ImportAsync(export, TestPassword);

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
    public async Task ImportAsyncShouldCorrectlyImportExportedData()
    {
        var workspace = new Workspace
        {
            Name = "Export Test Workspace",
            Description = "Full roundtrip test",
            IsPublic = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();

        var slackChannel = new NotificationChannel
        {
            WorkspaceId = workspace.Id,
            Name = "Slack Notifications",
            ChannelType = ChannelTypes.Slack,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Webhook.WebhookUrl] = JsonSerializer.SerializeToElement("https://hooks.slack.com/services/example")
            },
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var emailChannel = new NotificationChannel
        {
            WorkspaceId = workspace.Id,
            Name = "Email Notifications",
            ChannelType = ChannelTypes.Email,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Email.Recipients] = JsonSerializer.SerializeToElement("admin@example.com")
            },
            Enabled = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.NotificationChannels.AddRange(slackChannel, emailChannel);
        await DbContext.SaveChangesAsync();

        var slackSubscription = new EventSubscription
        {
            NotificationChannelId = slackChannel.Id,
            EventType = "CheckStatusChanged",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.EventSubscriptions.Add(slackSubscription);
        await DbContext.SaveChangesAsync();

        var httpCheck = new Check
        {
            WorkspaceId = workspace.Id,
            Name = "API Health Check",
            Description = "Monitors API health endpoint",
            CheckType = CheckTypes.Http,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.HttpCheck.Url] = JsonSerializer.SerializeToElement("https://api.example.com/health"),
                [ConfigurationKeys.HttpCheck.ExpectedStatusCodes] = JsonSerializer.SerializeToElement(new[] { 200, 201 })
            },
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var pingCheck = new Check
        {
            WorkspaceId = workspace.Id,
            Name = "Server Ping",
            Description = "Pings the server",
            CheckType = CheckTypes.Ping,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.PingCheck.Host] = JsonSerializer.SerializeToElement("server.example.com")
            },
            Schedule = "120",
            TimeoutSeconds = 10,
            Enabled = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Checks.AddRange(httpCheck, pingCheck);
        await DbContext.SaveChangesAsync();

        var httpAlert = new Alert
        {
            CheckId = httpCheck.Id,
            Name = "API Down Alert",
            TriggerOnWarn = false,
            TriggerOnDown = true,
            FailureThreshold = 3,
            SendRecoveryNotification = true,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        httpAlert.NotificationChannels.Add(slackChannel);
        httpAlert.NotificationChannels.Add(emailChannel);
        var pingAlert = new Alert
        {
            CheckId = pingCheck.Id,
            Name = "Server Unreachable",
            TriggerOnWarn = true,
            TriggerOnDown = true,
            FailureThreshold = 5,
            SendRecoveryNotification = false,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        pingAlert.NotificationChannels.Add(slackChannel);
        DbContext.Alerts.AddRange(httpAlert, pingAlert);
        await DbContext.SaveChangesAsync();

        var appStateService = new ApplicationStateService();
        var encryptionService = new AesEncryptionService();
        var exportService = new ConfigurationExportService(DbContext, appStateService, encryptionService);
        var exportData = await exportService.ExportAllAsync(TestPassword);

        DbContext.Workspaces.Remove(workspace);
        await DbContext.SaveChangesAsync();

        var importResult = await _importService.ImportAsync(exportData, TestPassword);

        Assert.IsTrue(importResult.Success);
        Assert.IsEmpty(importResult.Errors);
        Assert.AreEqual(1, importResult.WorkspacesCreated);
        Assert.AreEqual(2, importResult.ChecksCreated);
        Assert.AreEqual(2, importResult.NotificationChannelsCreated);
        Assert.AreEqual(2, importResult.AlertsCreated);

        var importedWorkspace = await DbContext.Workspaces
            .AsSplitQuery()
            .Include(w => w.Checks)
                .ThenInclude(c => c.Alerts)
                    .ThenInclude(a => a.NotificationChannels)
            .Include(w => w.NotificationChannels)
                .ThenInclude(nc => nc.EventSubscriptions)
            .FirstOrDefaultAsync(w => w.Name == "Export Test Workspace");

        Assert.IsNotNull(importedWorkspace);
        Assert.AreEqual("Full roundtrip test", importedWorkspace.Description);
        Assert.IsTrue(importedWorkspace.IsPublic);

        Assert.HasCount(2, importedWorkspace.NotificationChannels);
        var importedSlack = importedWorkspace.NotificationChannels.First(c => c.Name == "Slack Notifications");
        Assert.AreEqual(ChannelTypes.Slack, importedSlack.ChannelType);
        Assert.IsTrue(importedSlack.Enabled);
        Assert.IsTrue(importedSlack.ConfigurationJson.ContainsKey(ConfigurationKeys.Webhook.WebhookUrl));
        Assert.HasCount(1, importedSlack.EventSubscriptions);
        Assert.AreEqual("CheckStatusChanged", importedSlack.EventSubscriptions.First().EventType);

        var importedEmail = importedWorkspace.NotificationChannels.First(c => c.Name == "Email Notifications");
        Assert.AreEqual(ChannelTypes.Email, importedEmail.ChannelType);
        Assert.IsFalse(importedEmail.Enabled);

        Assert.HasCount(2, importedWorkspace.Checks);
        var importedHttpCheck = importedWorkspace.Checks.First(c => c.Name == "API Health Check");
        Assert.AreEqual(CheckTypes.Http, importedHttpCheck.CheckType);
        Assert.AreEqual("Monitors API health endpoint", importedHttpCheck.Description);
        Assert.AreEqual("60", importedHttpCheck.Schedule);
        Assert.AreEqual(30, importedHttpCheck.TimeoutSeconds);
        Assert.IsTrue(importedHttpCheck.Enabled);

        var importedPingCheck = importedWorkspace.Checks.First(c => c.Name == "Server Ping");
        Assert.AreEqual(CheckTypes.Ping, importedPingCheck.CheckType);
        Assert.AreEqual("120", importedPingCheck.Schedule);
        Assert.IsFalse(importedPingCheck.Enabled);

        Assert.HasCount(1, importedHttpCheck.Alerts);
        var importedHttpAlert = importedHttpCheck.Alerts.First();
        Assert.AreEqual("API Down Alert", importedHttpAlert.Name);
        Assert.IsFalse(importedHttpAlert.TriggerOnWarn);
        Assert.IsTrue(importedHttpAlert.TriggerOnDown);
        Assert.AreEqual(3, importedHttpAlert.FailureThreshold);
        Assert.IsTrue(importedHttpAlert.SendRecoveryNotification);
        Assert.IsTrue(importedHttpAlert.Enabled);
        Assert.HasCount(2, importedHttpAlert.NotificationChannels);
        Assert.IsTrue(importedHttpAlert.NotificationChannels.Any(c => c.Name == "Slack Notifications"));
        Assert.IsTrue(importedHttpAlert.NotificationChannels.Any(c => c.Name == "Email Notifications"));

        Assert.HasCount(1, importedPingCheck.Alerts);
        var importedPingAlert = importedPingCheck.Alerts.First();
        Assert.AreEqual("Server Unreachable", importedPingAlert.Name);
        Assert.IsTrue(importedPingAlert.TriggerOnWarn);
        Assert.IsTrue(importedPingAlert.TriggerOnDown);
        Assert.AreEqual(5, importedPingAlert.FailureThreshold);
        Assert.IsFalse(importedPingAlert.SendRecoveryNotification);
        Assert.HasCount(1, importedPingAlert.NotificationChannels);
        Assert.AreEqual("Slack Notifications", importedPingAlert.NotificationChannels.First().Name);
    }

    [TestMethod]
    public async Task ImportAsyncShouldScheduleEnabledChecks()
    {
        var export = CreateEncryptedExport(
        [
            new WorkspaceExportDto
            {
                Name = "Scheduler Test Workspace",
                IsPublic = true,
                Checks =
                [
                    new CheckExportDto
                    {
                        Name = "Enabled Check",
                        CheckType = CheckTypes.Http,
                        Configuration = new Dictionary<string, JsonElement>(),
                        Schedule = "60",
                        TimeoutSeconds = 30,
                        Enabled = true
                    },
                    new CheckExportDto
                    {
                        Name = "Disabled Check",
                        CheckType = CheckTypes.Ping,
                        Configuration = new Dictionary<string, JsonElement>(),
                        Schedule = "120",
                        TimeoutSeconds = 10,
                        Enabled = false
                    }
                ]
            }
        ]);

        var result = await _importService.ImportAsync(export, TestPassword);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.ChecksCreated);
        Assert.AreEqual(1, result.ChecksScheduled);

        await _mockScheduler.Received(1).ScheduleCheckAsync(
            Arg.Any<Guid>(),
            "60",
            Arg.Any<CancellationToken>());

        await _mockScheduler.DidNotReceive().ScheduleCheckAsync(
            Arg.Any<Guid>(),
            "120",
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task ImportAsyncShouldScheduleEnabledChecksWhenMerging()
    {
        var existingWorkspace = new Workspace
        {
            Name = "Merge Scheduler Test",
            Description = "Original",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Workspaces.Add(existingWorkspace);
        await DbContext.SaveChangesAsync();

        var export = CreateEncryptedExport(
        [
            new WorkspaceExportDto
            {
                Name = "Merge Scheduler Test",
                Description = "Updated",
                IsPublic = true,
                Checks =
                [
                    new CheckExportDto
                    {
                        Name = "Merged Enabled Check",
                        CheckType = CheckTypes.Tcp,
                        Configuration = new Dictionary<string, JsonElement>(),
                        Schedule = "90",
                        TimeoutSeconds = 15,
                        Enabled = true
                    }
                ]
            }
        ]);

        var result = await _importService.ImportAsync(export, TestPassword, ImportMergeStrategy.MergeIntoExisting);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, result.WorkspacesUpdated);
        Assert.AreEqual(1, result.ChecksCreated);
        Assert.AreEqual(1, result.ChecksScheduled);

        await _mockScheduler.Received(1).ScheduleCheckAsync(
            Arg.Any<Guid>(),
            "90",
            Arg.Any<CancellationToken>());
    }

    private static SamaExportDto CreateEncryptedExport(List<WorkspaceExportDto> workspaces)
    {
        var payloadJson = JsonSerializer.Serialize(workspaces);
        var encryptionService = new AesEncryptionService();
        var encryptedData = encryptionService.Encrypt(payloadJson, TestPassword);

        return new SamaExportDto
        {
            SchemaVersion = 2,
            ExportedFromVersion = "1.0.0",
            ExportedAt = DateTimeOffset.UtcNow,
            EncryptedData = encryptedData
        };
    }
}
