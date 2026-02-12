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
public class CheckCommandServiceTests : IntegrationTestBase
{
    private CheckCommandService _service = null!;
    private CheckSchedulerService _mockScheduler = null!;
    private EventSubscriptionService _mockEventService = null!;
    private Workspace _workspace = null!;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _workspace = await CreateWorkspaceAsync("Test Workspace");
        _mockScheduler = Substitute.For<CheckSchedulerService>(null, null, null);
        _mockEventService = Substitute.For<EventSubscriptionService>(null, null, null);

        var changeDetectionService = new CheckChangeDetectionService();

        _service = new CheckCommandService(
            DbContext,
            _mockScheduler,
            _mockEventService,
            changeDetectionService,
            Substitute.For<ILogger<CheckCommandService>>());
    }

    [TestMethod]
    public async Task CreateCheckAsyncShouldCreateCheckWithBasicProperties()
    {
        var config = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["url"] = System.Text.Json.JsonSerializer.SerializeToElement("https://example.com")
        };

        var checkId = await _service.CreateCheckAsync(
            _workspace.Id,
            "Test Check",
            "Test Description",
            CheckTypes.Http,
            "60",
            30,
            config,
            true,
            "admin");

        var check = await DbContext.Checks.FindAsync(checkId);
        Assert.IsNotNull(check);
        Assert.AreEqual("Test Check", check.Name);
        Assert.AreEqual("Test Description", check.Description);
        Assert.AreEqual(CheckTypes.Http, check.CheckType);
        Assert.AreEqual("60", check.Schedule);
        Assert.AreEqual(30, check.TimeoutSeconds);
        Assert.IsTrue(check.Enabled);
        Assert.AreEqual(_workspace.Id, check.WorkspaceId);
        Assert.AreEqual("https://example.com", check.ConfigurationJson["url"].ToString());
    }

    [TestMethod]
    public async Task CreateCheckAsyncShouldCreateDefaultAlert()
    {
        var checkId = await _service.CreateCheckAsync(
            _workspace.Id,
            "Check With Alert",
            null,
            CheckTypes.Tcp,
            "120",
            30,
            [],
            false,
            "admin");

        var alerts = DbContext.Alerts.Where(a => a.CheckId == checkId).ToList();
        Assert.HasCount(1, alerts);

        var defaultAlert = alerts[0];
        Assert.AreEqual("Check With Alert - Default Alert", defaultAlert.Name);
        Assert.IsTrue(defaultAlert.TriggerOnWarn);
        Assert.IsTrue(defaultAlert.TriggerOnDown);
        Assert.AreEqual(1, defaultAlert.FailureThreshold);
        Assert.IsTrue(defaultAlert.SendRecoveryNotification);
        Assert.IsTrue(defaultAlert.Enabled);
        Assert.IsEmpty(defaultAlert.NotificationChannels);
    }

    [TestMethod]
    public async Task CreateCheckAsyncShouldScheduleEnabledCheck()
    {
        var checkId = await _service.CreateCheckAsync(
            _workspace.Id,
            "Enabled Check",
            null,
            CheckTypes.Http,
            "90",
            30,
            [],
            true,
            "admin");

        await _mockScheduler.Received(1).ScheduleCheckAsync(checkId, "90");
    }

    [TestMethod]
    public async Task CreateCheckAsyncShouldNotScheduleDisabledCheck()
    {
        await _service.CreateCheckAsync(
            _workspace.Id,
            "Disabled Check",
            null,
            CheckTypes.Http,
            "60",
            30,
            [],
            false,
            "admin");

        await _mockScheduler.DidNotReceive().ScheduleCheckAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [TestMethod]
    public async Task CreateCheckAsyncShouldTriggerLifecycleEvent()
    {
        var checkId = await _service.CreateCheckAsync(
            _workspace.Id,
            "Event Check",
            null,
            CheckTypes.Tcp,
            "60",
            30,
            [],
            true,
            "testuser");

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.EventType == EventTypes.CheckCreated &&
                ctx.CheckId == checkId &&
                ctx.CheckName == "Event Check" &&
                ctx.CheckType == CheckTypes.Tcp &&
                ctx.WorkspaceName == "Test Workspace" &&
                ctx.PerformedBy == "testuser"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldReturnFalseWhenCheckDoesNotExist()
    {
        var result = await _service.UpdateCheckAsync(
            Guid.NewGuid(),
            "Updated Name",
            null,
            CheckTypes.Http,
            "60",
            30,
            [],
            true,
            "admin");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldUpdateCheckProperties()
    {
        var check = await CreateCheckAsync("Original Check", CheckTypes.Http, "60", true);
        var newConfig = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["host"] = System.Text.Json.JsonSerializer.SerializeToElement("example.com")
        };

        var result = await _service.UpdateCheckAsync(
            check.Id,
            "Updated Check",
            "New Description",
            CheckTypes.Tcp,
            "120",
            45,
            newConfig,
            false,
            "admin");

        Assert.IsTrue(result);

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Checks.FindAsync(check.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual("Updated Check", updated.Name);
        Assert.AreEqual("New Description", updated.Description);
        Assert.AreEqual(CheckTypes.Tcp, updated.CheckType);
        Assert.AreEqual("120", updated.Schedule);
        Assert.AreEqual(45, updated.TimeoutSeconds);
        Assert.IsFalse(updated.Enabled);
        Assert.AreEqual("example.com", updated.ConfigurationJson["host"].ToString());
        Assert.IsTrue(updated.UpdatedAt > check.UpdatedAt);
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldScheduleWhenEnabling()
    {
        var check = await CreateCheckAsync("Disabled Check", CheckTypes.Http, "60", false);

        await _service.UpdateCheckAsync(
            check.Id,
            "Enabled Check",
            null,
            CheckTypes.Http,
            "90",
            30,
            [],
            true,
            "admin");

        await _mockScheduler.Received(1).ScheduleCheckAsync(check.Id, "90");
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldUnscheduleWhenDisabling()
    {
        var check = await CreateCheckAsync("Enabled Check", CheckTypes.Http, "60", true);

        await _service.UpdateCheckAsync(
            check.Id,
            "Disabled Check",
            null,
            CheckTypes.Http,
            "60",
            30,
            [],
            false,
            "admin");

        await _mockScheduler.Received(1).UnscheduleCheckAsync(check.Id);
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldRescheduleWhenIntervalChanges()
    {
        var check = await CreateCheckAsync("Scheduled Check", CheckTypes.Http, "60", true);

        await _service.UpdateCheckAsync(
            check.Id,
            "Rescheduled Check",
            null,
            CheckTypes.Http,
            "120",
            30,
            [],
            true,
            "admin");

        await _mockScheduler.Received(1).ScheduleCheckAsync(check.Id, "120");
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldNotUnscheduleWhenStillEnabled()
    {
        var check = await CreateCheckAsync("Enabled Check", CheckTypes.Http, "60", true);

        await _service.UpdateCheckAsync(
            check.Id,
            "Updated Check",
            null,
            CheckTypes.Http,
            "90",
            30,
            [],
            true,
            "admin");

        await _mockScheduler.DidNotReceive().UnscheduleCheckAsync(Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldTriggerLifecycleEvent()
    {
        var check = await CreateCheckAsync("Original Check", CheckTypes.Http, "60", true);

        await _service.UpdateCheckAsync(
            check.Id,
            "Updated Check",
            null,
            CheckTypes.Tcp,
            "120",
            30,
            [],
            false,
            "testuser");

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.EventType == EventTypes.CheckUpdated &&
                ctx.CheckId == check.Id &&
                ctx.CheckName == "Updated Check" &&
                ctx.CheckType == CheckTypes.Tcp &&
                ctx.WorkspaceName == "Test Workspace" &&
                ctx.PerformedBy == "testuser" &&
                ctx.ConfigurationChanges != null &&
                ctx.ConfigurationChanges.Count > 0),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldIncludeChangedFieldsInConfigurationChanges()
    {
        var originalConfig = new Dictionary<string, System.Text.Json.JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = System.Text.Json.JsonSerializer.SerializeToElement("https://example.com")
        };
        var check = await CreateCheckAsync("Test Check", CheckTypes.Http, "60", true, originalConfig);

        var newConfig = new Dictionary<string, System.Text.Json.JsonElement>
        {
            [ConfigurationKeys.HttpCheck.Url] = System.Text.Json.JsonSerializer.SerializeToElement("https://newexample.com")
        };

        await _service.UpdateCheckAsync(
            check.Id,
            "Test Check Updated",
            "New description",
            CheckTypes.Http,
            "120",
            45,
            newConfig,
            false,
            "testuser");

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.ConfigurationChanges != null &&
                ctx.ConfigurationChanges.ContainsKey("Name") &&
                ctx.ConfigurationChanges.ContainsKey("Description") &&
                ctx.ConfigurationChanges.ContainsKey("Schedule") &&
                ctx.ConfigurationChanges.ContainsKey("Timeout") &&
                ctx.ConfigurationChanges.ContainsKey("Enabled") &&
                ctx.ConfigurationChanges.ContainsKey("Updated At") &&
                ctx.ConfigurationChanges.ContainsKey("URL")),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldIncludeUpdatedAtInConfigurationChanges()
    {
        var check = await CreateCheckAsync("Test Check", CheckTypes.Http, "60", true);

        await _service.UpdateCheckAsync(
            check.Id,
            "Test Check",
            check.Description,
            check.CheckType,
            check.Schedule,
            check.TimeoutSeconds,
            check.ConfigurationJson,
            check.Enabled,
            "testuser");

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.ConfigurationChanges != null &&
                ctx.ConfigurationChanges.ContainsKey("Updated At") &&
                ctx.ConfigurationChanges.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task DeleteCheckAsyncShouldReturnFalseWhenCheckDoesNotExist()
    {
        var result = await _service.DeleteCheckAsync(Guid.NewGuid(), "admin");

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task DeleteCheckAsyncShouldDeleteCheck()
    {
        var check = await CreateCheckAsync("Check To Delete", CheckTypes.Http, "60", false);

        var result = await _service.DeleteCheckAsync(check.Id, "admin");

        Assert.IsTrue(result);

        var deleted = await DbContext.Checks.FindAsync(check.Id);
        Assert.IsNull(deleted);
    }

    [TestMethod]
    public async Task DeleteCheckAsyncShouldUnscheduleEnabledCheck()
    {
        var check = await CreateCheckAsync("Enabled Check", CheckTypes.Http, "60", true);

        await _service.DeleteCheckAsync(check.Id, "admin");

        await _mockScheduler.Received(1).UnscheduleCheckAsync(check.Id);
    }

    [TestMethod]
    public async Task DeleteCheckAsyncShouldNotUnscheduleDisabledCheck()
    {
        var check = await CreateCheckAsync("Disabled Check", CheckTypes.Http, "60", false);

        await _service.DeleteCheckAsync(check.Id, "admin");

        await _mockScheduler.DidNotReceive().UnscheduleCheckAsync(Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task DeleteCheckAsyncShouldDeleteRelatedAlerts()
    {
        var check = await CreateCheckAsync("Check With Alerts", CheckTypes.Http, "60", false);
        var alert = new Alert
        {
            CheckId = check.Id,
            Name = "Test Alert",
            TriggerOnDown = true,
            FailureThreshold = 1,
            SendRecoveryNotification = true,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        DbContext.Alerts.Add(alert);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await _service.DeleteCheckAsync(check.Id, "admin");

        var deletedAlert = await DbContext.Alerts.FindAsync(alert.Id);
        Assert.IsNull(deletedAlert);
    }

    [TestMethod]
    public async Task DeleteCheckAsyncShouldDeleteRelatedCheckResults()
    {
        var check = await CreateCheckAsync("Check With Results", CheckTypes.Http, "60", false);
        var result = new CheckResult
        {
            CheckId = check.Id,
            Status = CheckStatuses.Up,
            CheckedAt = DateTimeOffset.UtcNow
        };
        DbContext.CheckResults.Add(result);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await _service.DeleteCheckAsync(check.Id, "admin");

        var deletedResult = await DbContext.CheckResults.FindAsync(result.Id);
        Assert.IsNull(deletedResult);
    }

    [TestMethod]
    public async Task DeleteCheckAsyncShouldTriggerLifecycleEvent()
    {
        var check = await CreateCheckAsync("Check To Delete", CheckTypes.Tcp, "60", true);
        var checkId = check.Id;

        await _service.DeleteCheckAsync(checkId, "testuser");

        await _mockEventService.Received(1).TriggerLifecycleEventAsync(
            _workspace.Id,
            Arg.Is<LifecycleEventContext>(ctx =>
                ctx.EventType == EventTypes.CheckDeleted &&
                ctx.CheckId == checkId &&
                ctx.CheckName == "Check To Delete" &&
                ctx.CheckType == CheckTypes.Tcp &&
                ctx.WorkspaceName == "Test Workspace" &&
                ctx.PerformedBy == "testuser"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task CreateCheckAsyncShouldSupportMultipleChecksInSameWorkspace()
    {
        var check1Id = await _service.CreateCheckAsync(
            _workspace.Id, "Check 1", null, CheckTypes.Http, "60", 30, [], true, "admin");

        var check2Id = await _service.CreateCheckAsync(
            _workspace.Id, "Check 2", null, CheckTypes.Tcp, "120", 45, [], false, "admin");

        var checks = DbContext.Checks.Where(c => c.WorkspaceId == _workspace.Id).ToList();
        Assert.HasCount(2, checks);
        Assert.IsTrue(checks.Any(c => c.Name == "Check 1"));
        Assert.IsTrue(checks.Any(c => c.Name == "Check 2"));
    }

    [TestMethod]
    public async Task UpdateCheckAsyncShouldPreserveWorkspaceId()
    {
        var check = await CreateCheckAsync("Original Check", CheckTypes.Http, "60", true);
        var originalWorkspaceId = check.WorkspaceId;

        await _service.UpdateCheckAsync(
            check.Id,
            "Updated Check",
            null,
            CheckTypes.Tcp,
            "120",
            30,
            [],
            false,
            "admin");

        DbContext.ChangeTracker.Clear();
        var updated = await DbContext.Checks.FindAsync(check.Id);
        Assert.IsNotNull(updated);
        Assert.AreEqual(originalWorkspaceId, updated.WorkspaceId);
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

    private async Task<Check> CreateCheckAsync(string name, string checkType, string schedule, bool enabled, Dictionary<string, System.Text.Json.JsonElement>? config = null)
    {
        var check = new Check
        {
            WorkspaceId = _workspace.Id,
            Name = name,
            CheckType = checkType,
            ConfigurationJson = config ?? [],
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
}
