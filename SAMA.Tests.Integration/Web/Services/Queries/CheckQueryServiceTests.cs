using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Integration.Web.Services.Queries;

[TestClass]
public class CheckQueryServiceTests : IntegrationTestBase
{
    private CheckQueryService _service = null!;
    private ApplicationStateService _mockAppState = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private SensitiveDataMaskingService _mockMaskingService = null!;
    private Workspace _workspace = null!;
    private DateTimeOffset _testStartTime;

    [TestInitialize]
    public override async Task InitializeTestAsync()
    {
        await base.InitializeTestAsync();

        _testStartTime = DateTimeOffset.UtcNow;
        _workspace = await CreateWorkspaceAsync("Test Workspace");
        _mockAppState = Substitute.For<ApplicationStateService>();
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!);
        _mockMaskingService = Substitute.For<SensitiveDataMaskingService>();

        _mockMaskingService.MaskCheckConfig(Arg.Any<string>(), Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>())
            .Returns(new Dictionary<string, object> { ["test"] = "masked" });

        _mockGlobalSettings.TimeZone.Returns("UTC");

        _mockAppState.StartupTime.Returns(_testStartTime.AddMinutes(-10));

        _service = new CheckQueryService(DbContext, _mockAppState, _mockGlobalSettings, _mockMaskingService);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldReturnEmptyListWhenNoChecks()
    {
        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldReturnChecksOrderedByStatusThenName()
    {
        var downCheck = await CreateCheckAsync("Zebra Down", CheckTypes.Http, "60", true);
        var warnCheck = await CreateCheckAsync("Alpha Warn", CheckTypes.Http, "60", true);
        var upCheck = await CreateCheckAsync("Beta Up", CheckTypes.Http, "60", true);
        var pendingCheck = await CreateCheckAsync("Charlie Pending", CheckTypes.Http, "60", true);
        var disabledCheck = await CreateCheckAsync("Delta Disabled", CheckTypes.Http, "60", false);
        var anotherDownCheck = await CreateCheckAsync("Another Down", CheckTypes.Http, "60", true);

        await CreateCheckResultAsync(downCheck.Id, CheckStatuses.Down, _testStartTime.AddMinutes(5));
        await CreateCheckResultAsync(warnCheck.Id, CheckStatuses.Warn, _testStartTime.AddMinutes(5));
        await CreateCheckResultAsync(upCheck.Id, CheckStatuses.Up, _testStartTime.AddMinutes(5));
        await CreateCheckResultAsync(anotherDownCheck.Id, CheckStatuses.Down, _testStartTime.AddMinutes(5));

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(6, result);
        Assert.AreEqual("Another Down", result[0].Name);
        Assert.AreEqual(CheckStatuses.Down, result[0].LastStatus);
        Assert.AreEqual("Zebra Down", result[1].Name);
        Assert.AreEqual(CheckStatuses.Down, result[1].LastStatus);
        Assert.AreEqual("Alpha Warn", result[2].Name);
        Assert.AreEqual(CheckStatuses.Warn, result[2].LastStatus);
        Assert.AreEqual("Beta Up", result[3].Name);
        Assert.AreEqual(CheckStatuses.Up, result[3].LastStatus);
        Assert.AreEqual("Charlie Pending", result[4].Name);
        Assert.IsNull(result[4].LastStatus);
        Assert.AreEqual("Delta Disabled", result[5].Name);
        Assert.IsNull(result[5].LastStatus);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldSetNullStatusForEnabledCheckWithoutResults()
    {
        await CreateCheckAsync("Pending Check", CheckTypes.Http, "60", true);

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.IsNull(result[0].LastStatus);
        Assert.IsNull(result[0].LastCheckedAt);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldSetNullStatusWhenResultBeforeStartup()
    {
        var check = await CreateCheckAsync("Old Result Check", CheckTypes.Http, "60", true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-20));

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.IsNull(result[0].LastStatus);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldSetNullStatusWhenCheckUpdatedAfterLastResult()
    {
        var check = await CreateCheckAsync("Updated Check", CheckTypes.Http, "60", true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-5));

        check.UpdatedAt = _testStartTime.AddMinutes(-3);
        DbContext.Checks.Update(check);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.IsNull(result[0].LastStatus);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldReturnMostRecentResult()
    {
        var check = await CreateCheckAsync("Multi Result Check", CheckTypes.Http, "60", true);
        check.UpdatedAt = _testStartTime.AddMinutes(-10);
        DbContext.Checks.Update(check);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-9));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Warn, _testStartTime.AddMinutes(-8));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, _testStartTime.AddMinutes(-7));

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.AreEqual(CheckStatuses.Down, result[0].LastStatus);
        Assert.IsNotNull(result[0].LastCheckedAt);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldNotShowStatusForDisabledCheckWithResults()
    {
        var check = await CreateCheckAsync("Disabled Check", CheckTypes.Http, "60", false);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-5));

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.IsFalse(result[0].Enabled);
        Assert.IsNull(result[0].LastStatus);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldIncludeAlertCount()
    {
        var check = await CreateCheckAsync("Check With Alerts", CheckTypes.Http, "60", true);
        await CreateAlertAsync(check.Id, "Alert 1");
        await CreateAlertAsync(check.Id, "Alert 2");

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.AreEqual(2, result[0].AlertCount);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldOnlyReturnChecksForSpecifiedWorkspace()
    {
        var otherWorkspace = await CreateWorkspaceAsync("Other Workspace");
        await CreateCheckAsync("Workspace 1 Check", CheckTypes.Http, "60", true);
        await CreateCheckAsync("Other Check", CheckTypes.Tcp, "120", true, workspaceId: otherWorkspace.Id);

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.AreEqual("Workspace 1 Check", result[0].Name);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldIncludeResponseTimeAndErrorMessage()
    {
        var check = await CreateCheckAsync("Check With Details", CheckTypes.Http, "60", true);
        check.UpdatedAt = _testStartTime.AddMinutes(-10);
        DbContext.Checks.Update(check);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, _testStartTime.AddMinutes(-5), errorMessage: "Connection timeout", responseTimeMs: 5000);

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.AreEqual(CheckStatuses.Down, result[0].LastStatus);
        Assert.AreEqual(5000, result[0].LastResponseTimeMs);
        Assert.AreEqual("Connection timeout", result[0].LastErrorMessage);
    }

    [TestMethod]
    public async Task GetChecksForWorkspaceAsyncShouldReturnNullResponseTimeAndErrorForCheckWithoutResults()
    {
        await CreateCheckAsync("No Results Check", CheckTypes.Http, "60", true);

        var result = await _service.GetChecksForWorkspaceAsync(_workspace.Id);

        Assert.HasCount(1, result);
        Assert.IsNull(result[0].LastResponseTimeMs);
        Assert.IsNull(result[0].LastErrorMessage);
    }

    [TestMethod]
    public async Task GetCheckDetailsAsyncShouldReturnNullWhenCheckDoesNotExist()
    {
        var result = await _service.GetCheckDetailsAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetCheckDetailsAsyncShouldReturnCheckWithBasicProperties()
    {
        var check = await CreateCheckAsync("Details Check", CheckTypes.Http, "60", true, description: "Test Description");

        var result = await _service.GetCheckDetailsAsync(check.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(check.Id, result.Id);
        Assert.AreEqual("Details Check", result.Name);
        Assert.AreEqual("Test Description", result.Description);
        Assert.AreEqual(CheckTypes.Http, result.CheckType);
        Assert.AreEqual("60", result.Schedule);
        Assert.AreEqual(30, result.TimeoutSeconds);
        Assert.IsTrue(result.Enabled);
        Assert.AreEqual(_workspace.Id, result.WorkspaceId);
        Assert.AreEqual("Test Workspace", result.WorkspaceName);
    }

    [TestMethod]
    public async Task GetCheckDetailsAsyncShouldIncludeMaskedConfiguration()
    {
        var maskedConfig = new Dictionary<string, object> { ["url"] = "https://example.com", ["password"] = "***" };
        _mockMaskingService.MaskCheckConfig(Arg.Any<string>(), Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>())
            .Returns(maskedConfig);

        var check = await CreateCheckAsync("Config Check", CheckTypes.Http, "60", true);

        var result = await _service.GetCheckDetailsAsync(check.Id);

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.MaskedConfiguration);
        Assert.AreEqual(maskedConfig, result.MaskedConfiguration);
    }

    [TestMethod]
    public async Task GetCheckDetailsAsyncShouldIncludeResultCount()
    {
        var check = await CreateCheckAsync("Result Count Check", CheckTypes.Http, "60", true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-15));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-10));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, _testStartTime.AddMinutes(-5));

        var result = await _service.GetCheckDetailsAsync(check.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(3, result.ResultCount);
    }

    [TestMethod]
    public async Task GetCheckDetailsAsyncShouldIncludeAlertsOrderedByName()
    {
        var check = await CreateCheckAsync("Alert Details Check", CheckTypes.Http, "60", true);
        await CreateDetailedAlertAsync(check.Id, "Zebra Alert", true, false, 1, true, true, 1);
        await CreateDetailedAlertAsync(check.Id, "Alpha Alert", false, true, 3, false, false, 2);

        var result = await _service.GetCheckDetailsAsync(check.Id);

        Assert.IsNotNull(result);
        Assert.HasCount(2, result.Alerts);
        Assert.AreEqual("Alpha Alert", result.Alerts[0].Name);
        Assert.AreEqual("Zebra Alert", result.Alerts[1].Name);

        var alpha = result.Alerts[0];
        Assert.IsFalse(alpha.TriggerOnWarn);
        Assert.IsTrue(alpha.TriggerOnDown);
        Assert.AreEqual(3, alpha.FailureThreshold);
        Assert.IsFalse(alpha.SendRecoveryNotification);
        Assert.IsFalse(alpha.Enabled);
        Assert.AreEqual(2, alpha.ChannelCount);
    }

    [TestMethod]
    public async Task GetCheckForEditAsyncShouldReturnNullWhenCheckDoesNotExist()
    {
        var result = await _service.GetCheckForEditAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetCheckForEditAsyncShouldReturnCheckEditViewModel()
    {
        var configJson = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["url"] = System.Text.Json.JsonSerializer.SerializeToElement("https://example.com")
        };
        var check = await CreateCheckAsync("Edit Check", CheckTypes.Http, "90", false, "Edit Description", configJson);

        var result = await _service.GetCheckForEditAsync(check.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(check.Id, result.Id);
        Assert.AreEqual("Edit Check", result.Name);
        Assert.AreEqual("Edit Description", result.Description);
        Assert.AreEqual(CheckTypes.Http, result.CheckType);
        Assert.AreEqual("90", result.Schedule);
        Assert.AreEqual(30, result.TimeoutSeconds);
        Assert.IsFalse(result.Enabled);
        Assert.AreEqual(_workspace.Id, result.WorkspaceId);
        Assert.AreEqual("Test Workspace", result.WorkspaceName);
        Assert.IsNotNull(result.ConfigurationJson);
        Assert.IsTrue(result.ConfigurationJson.ContainsKey("url"));
    }

    [TestMethod]
    public async Task GetCheckHistoryAsyncShouldReturnEmptyListWhenNoResults()
    {
        var check = await CreateCheckAsync("History Check", CheckTypes.Http, "60", true);

        var result = await _service.GetCheckHistoryAsync(check.Id, 24);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task GetCheckHistoryAsyncShouldReturnHistoryOrderedByTimestamp()
    {
        var check = await CreateCheckAsync("History Check", CheckTypes.Http, "60", true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddHours(-3), responseTimeMs: 300);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Warn, _testStartTime.AddHours(-1), responseTimeMs: 100);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, _testStartTime.AddHours(-2), responseTimeMs: 200, errorMessage: "Error");

        var result = await _service.GetCheckHistoryAsync(check.Id, 24);

        Assert.HasCount(3, result);
        Assert.AreEqual(300, result[0].ResponseTimeMs);
        Assert.AreEqual(200, result[1].ResponseTimeMs);
        Assert.AreEqual(100, result[2].ResponseTimeMs);
        Assert.AreEqual("Error", result[1].ErrorMessage);
    }

    [TestMethod]
    public async Task GetCheckHistoryAsyncShouldFilterByTimeRange()
    {
        var check = await CreateCheckAsync("Time Range Check", CheckTypes.Http, "60", true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddHours(-50), responseTimeMs: 100);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddHours(-12), responseTimeMs: 200);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddHours(-1), responseTimeMs: 300);

        var result = await _service.GetCheckHistoryAsync(check.Id, 24);

        Assert.HasCount(2, result);
        Assert.AreEqual(200, result[0].ResponseTimeMs);
        Assert.AreEqual(300, result[1].ResponseTimeMs);
    }

    [TestMethod]
    public async Task GetCheckHistoryAsyncShouldClampHoursToMaximum()
    {
        var check = await CreateCheckAsync("Max Hours Check", CheckTypes.Http, "60", true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddHours(-200));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddHours(-160));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddHours(-100));

        var result = await _service.GetCheckHistoryAsync(check.Id, 999);

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task GetCheckUptimeAsyncShouldReturnNullWhenNoResults()
    {
        var check = await CreateCheckAsync("Uptime Check", CheckTypes.Http, "60", true);

        var result = await _service.GetCheckUptimeAsync(check.Id, 24);

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetCheckUptimeAsyncShouldReturn100PercentWhenOnlyOneUpResult()
    {
        var check = await CreateCheckAsync("Uptime Check", CheckTypes.Http, "60", true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddHours(-1));

        var result = await _service.GetCheckUptimeAsync(check.Id, 24);

        Assert.IsNotNull(result);
        Assert.AreEqual(100.0, result.UptimePercentage);
        Assert.AreEqual(1, result.TotalChecks);
        Assert.AreEqual(1, result.UpCount);
        Assert.AreEqual(0, result.WarnCount);
        Assert.AreEqual(0, result.DownCount);
    }

    [TestMethod]
    public async Task GetCheckUptimeAsyncShouldReturn100PercentWhenOnlyOneWarnResult()
    {
        var check = await CreateCheckAsync("Uptime Check", CheckTypes.Http, "60", true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Warn, _testStartTime.AddHours(-1));

        var result = await _service.GetCheckUptimeAsync(check.Id, 24);

        Assert.IsNotNull(result);
        Assert.AreEqual(100.0, result.UptimePercentage);
        Assert.AreEqual(1, result.TotalChecks);
        Assert.AreEqual(0, result.UpCount);
        Assert.AreEqual(1, result.WarnCount);
        Assert.AreEqual(0, result.DownCount);
    }

    [TestMethod]
    public async Task GetCheckUptimeAsyncShouldReturn0PercentWhenOnlyOneDownResult()
    {
        var check = await CreateCheckAsync("Uptime Check", CheckTypes.Http, "60", true);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, _testStartTime.AddHours(-1));

        var result = await _service.GetCheckUptimeAsync(check.Id, 24);

        Assert.IsNotNull(result);
        Assert.AreEqual(0.0, result.UptimePercentage);
        Assert.AreEqual(1, result.TotalChecks);
        Assert.AreEqual(0, result.UpCount);
        Assert.AreEqual(0, result.WarnCount);
        Assert.AreEqual(1, result.DownCount);
    }

    [TestMethod]
    public async Task GetCheckUptimeAsyncShouldCalculateTimeBasedUptime()
    {
        var check = await CreateCheckAsync("Uptime Check", CheckTypes.Http, "60", true);

        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-60));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-50));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-40));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, _testStartTime.AddMinutes(-30));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, _testStartTime.AddMinutes(-20));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-10));

        var result = await _service.GetCheckUptimeAsync(check.Id, 24);

        Assert.IsNotNull(result);
        Assert.AreEqual(6, result.TotalChecks);
        Assert.AreEqual(4, result.UpCount);
        Assert.AreEqual(0, result.WarnCount);
        Assert.AreEqual(2, result.DownCount);
    }

    [TestMethod]
    public async Task GetCheckUptimeAsyncShouldCountWarnAsUptime()
    {
        var check = await CreateCheckAsync("Uptime Check", CheckTypes.Http, "60", true);

        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-40));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Warn, _testStartTime.AddMinutes(-30));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, _testStartTime.AddMinutes(-20));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddMinutes(-10));

        var result = await _service.GetCheckUptimeAsync(check.Id, 24);

        Assert.IsNotNull(result);
        Assert.AreEqual(4, result.TotalChecks);
        Assert.AreEqual(2, result.UpCount);
        Assert.AreEqual(1, result.WarnCount);
        Assert.AreEqual(1, result.DownCount);
    }

    [TestMethod]
    public async Task GetCheckUptimeAsyncShouldExtendLastStateToNow()
    {
        var check = await CreateCheckAsync("Uptime Check", CheckTypes.Http, "60", true);

        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, _testStartTime.AddSeconds(-70));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, _testStartTime.AddSeconds(-10));

        var result = await _service.GetCheckUptimeAsync(check.Id, 24);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.TotalChecks);
        Assert.AreEqual(1, result.UpCount);
        Assert.AreEqual(0, result.WarnCount);
        Assert.AreEqual(1, result.DownCount);
        Assert.IsGreaterThan(0.0, result.UptimePercentage);
        Assert.IsLessThan(100.0, result.UptimePercentage);
    }

    [TestMethod]
    public async Task GetCheckBasicInfoAsyncShouldReturnNullWhenCheckDoesNotExist()
    {
        var result = await _service.GetCheckBasicInfoAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task GetCheckBasicInfoAsyncShouldReturnBasicCheckInfo()
    {
        var check = await CreateCheckAsync("Basic Info Check", CheckTypes.Http, "60", true, "Test Description");

        var result = await _service.GetCheckBasicInfoAsync(check.Id);

        Assert.IsNotNull(result);
        Assert.AreEqual(check.Id, result.Id);
        Assert.AreEqual("Basic Info Check", result.Name);
        Assert.AreEqual(_workspace.Id, result.WorkspaceId);
        Assert.AreEqual("Test Workspace", result.WorkspaceName);
    }

    [TestMethod]
    public async Task GetCheckBasicInfoAsyncShouldReturnCheckFromCorrectWorkspace()
    {
        var otherWorkspace = await CreateWorkspaceAsync("Other Workspace");
        var check1 = await CreateCheckAsync("Workspace 1 Check", CheckTypes.Http, "60", true);
        var check2 = await CreateCheckAsync("Workspace 2 Check", CheckTypes.Tcp, "120", false, workspaceId: otherWorkspace.Id);

        var result1 = await _service.GetCheckBasicInfoAsync(check1.Id);
        var result2 = await _service.GetCheckBasicInfoAsync(check2.Id);

        Assert.IsNotNull(result1);
        Assert.AreEqual("Workspace 1 Check", result1.Name);
        Assert.AreEqual(_workspace.Id, result1.WorkspaceId);
        Assert.AreEqual("Test Workspace", result1.WorkspaceName);

        Assert.IsNotNull(result2);
        Assert.AreEqual("Workspace 2 Check", result2.Name);
        Assert.AreEqual(otherWorkspace.Id, result2.WorkspaceId);
        Assert.AreEqual("Other Workspace", result2.WorkspaceName);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldReturnEmptyTimelineWhenNoChecks()
    {
        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24);

        Assert.IsNotNull(result);
        Assert.IsEmpty(result.Increments);
        Assert.AreEqual(24, result.Hours);
        Assert.AreEqual(30, result.IncrementMinutes);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldCreateTimeBasedIncrements()
    {
        var check = await CreateCheckAsync("Timeline Check", CheckTypes.Http, "60", true);

        var referenceTime = DateTimeOffset.UtcNow;
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, referenceTime.AddHours(-3));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, referenceTime.AddHours(-1));

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);
        Assert.AreEqual(24, result.Hours);
        Assert.AreEqual(30, result.IncrementMinutes);
        Assert.AreEqual(1, result.Increments[0].TotalChecks);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldAggregateCheckStatusesPerIncrement()
    {
        var check1 = await CreateCheckAsync("Check 1", CheckTypes.Http, "60", true);
        var check2 = await CreateCheckAsync("Check 2", CheckTypes.Http, "60", true);
        var check3 = await CreateCheckAsync("Check 3", CheckTypes.Http, "60", true);

        var referenceTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        await CreateCheckResultAsync(check1.Id, CheckStatuses.Up, referenceTime);
        await CreateCheckResultAsync(check2.Id, CheckStatuses.Warn, referenceTime, errorMessage: "Warning message");
        await CreateCheckResultAsync(check3.Id, CheckStatuses.Down, referenceTime, errorMessage: "Error message");

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);

        var increment = result.Increments.First(i => i.StartTime <= referenceTime && referenceTime < i.EndTime);
        Assert.AreEqual(3, increment.TotalChecks);
        Assert.AreEqual(1, increment.UpCount);
        Assert.AreEqual(1, increment.WarnCount);
        Assert.AreEqual(1, increment.DownCount);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldTrackChecksInWarnState()
    {
        var check1 = await CreateCheckAsync("Warning Check 1", CheckTypes.Http, "60", true);
        var check2 = await CreateCheckAsync("Warning Check 2", CheckTypes.Http, "60", true);

        var referenceTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        await CreateCheckResultAsync(check1.Id, CheckStatuses.Warn, referenceTime, errorMessage: "Slow response");
        await CreateCheckResultAsync(check2.Id, CheckStatuses.Warn, referenceTime, errorMessage: "High latency");

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);

        var increment = result.Increments.First(i => i.StartTime <= referenceTime && referenceTime < i.EndTime);
        Assert.AreEqual(2, increment.WarnCount);
        Assert.HasCount(2, increment.ChecksInWarn);
        Assert.IsTrue(increment.ChecksInWarn.Any(c => c.CheckName == "Warning Check 1" && c.ErrorMessage == "Slow response"));
        Assert.IsTrue(increment.ChecksInWarn.Any(c => c.CheckName == "Warning Check 2" && c.ErrorMessage == "High latency"));
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldTrackChecksInDownState()
    {
        var check1 = await CreateCheckAsync("Down Check 1", CheckTypes.Http, "60", true);
        var check2 = await CreateCheckAsync("Down Check 2", CheckTypes.Http, "60", true);

        var referenceTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        await CreateCheckResultAsync(check1.Id, CheckStatuses.Down, referenceTime, errorMessage: "Connection refused");
        await CreateCheckResultAsync(check2.Id, CheckStatuses.Down, referenceTime, errorMessage: "Timeout");

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);

        var increment = result.Increments.First(i => i.StartTime <= referenceTime && referenceTime < i.EndTime);
        Assert.AreEqual(2, increment.DownCount);
        Assert.HasCount(2, increment.ChecksInDown);
        Assert.IsTrue(increment.ChecksInDown.Any(c => c.CheckName == "Down Check 1" && c.ErrorMessage == "Connection refused"));
        Assert.IsTrue(increment.ChecksInDown.Any(c => c.CheckName == "Down Check 2" && c.ErrorMessage == "Timeout"));
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldUseMostSevereStatusWithinIncrement()
    {
        var check = await CreateCheckAsync("Status Change Check", CheckTypes.Http, "60", true);

        var referenceTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, referenceTime);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, referenceTime.AddSeconds(5));
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, referenceTime.AddSeconds(10));

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);

        var increment = result.Increments.First(i => i.StartTime <= referenceTime && referenceTime < i.EndTime);
        Assert.AreEqual(0, increment.UpCount);
        Assert.AreEqual(0, increment.WarnCount);
        Assert.AreEqual(1, increment.DownCount);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldPrioritizeDownOverWarn()
    {
        var check = await CreateCheckAsync("Severity Check", CheckTypes.Http, "60", true);

        var referenceTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Warn, referenceTime, errorMessage: "Slow");
        await CreateCheckResultAsync(check.Id, CheckStatuses.Down, referenceTime.AddSeconds(5), errorMessage: "Failed");
        await CreateCheckResultAsync(check.Id, CheckStatuses.Warn, referenceTime.AddSeconds(10), errorMessage: "Slow again");

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);

        var increment = result.Increments.First(i => i.StartTime <= referenceTime && referenceTime < i.EndTime);
        Assert.AreEqual(0, increment.UpCount);
        Assert.AreEqual(0, increment.WarnCount);
        Assert.AreEqual(1, increment.DownCount);
        Assert.HasCount(1, increment.ChecksInDown);
        Assert.AreEqual("Failed", increment.ChecksInDown[0].ErrorMessage);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldPrioritizeWarnOverUp()
    {
        var check = await CreateCheckAsync("Severity Check", CheckTypes.Http, "60", true);

        var referenceTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, referenceTime);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Warn, referenceTime.AddSeconds(5), errorMessage: "Warning");
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, referenceTime.AddSeconds(10));

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);

        var increment = result.Increments.First(i => i.StartTime <= referenceTime && referenceTime < i.EndTime);
        Assert.AreEqual(0, increment.UpCount);
        Assert.AreEqual(1, increment.WarnCount);
        Assert.AreEqual(0, increment.DownCount);
        Assert.HasCount(1, increment.ChecksInWarn);
        Assert.AreEqual("Warning", increment.ChecksInWarn[0].ErrorMessage);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldAdjustIncrementSizeBasedOnHours()
    {
        var check = await CreateCheckAsync("Time Range Check", CheckTypes.Http, "60", true);
        var referenceTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        await CreateCheckResultAsync(check.Id, CheckStatuses.Up, referenceTime);

        var result3h = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 3);
        Assert.AreEqual(5, result3h.IncrementMinutes);

        var result6h = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 6);
        Assert.AreEqual(10, result6h.IncrementMinutes);

        var result24h = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24);
        Assert.AreEqual(30, result24h.IncrementMinutes);

        var result72h = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 72);
        Assert.AreEqual(60, result72h.IncrementMinutes);

        var result168h = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 168);
        Assert.AreEqual(120, result168h.IncrementMinutes);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldLimitToMaxChecks()
    {
        var referenceTime = DateTimeOffset.UtcNow.AddMinutes(-10);
        for (int i = 0; i < 20; i++)
        {
            var check = await CreateCheckAsync($"Check {i}", CheckTypes.Http, "60", true);
            await CreateCheckResultAsync(check.Id, CheckStatuses.Up, referenceTime);
        }

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 24, maxChecks: 10);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);
        Assert.AreEqual(10, result.Increments[0].TotalChecks);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldOnlyIncludeEnabledChecks()
    {
        var enabledCheck = await CreateCheckAsync("Enabled Check", CheckTypes.Http, "60", true);
        var disabledCheck = await CreateCheckAsync("Disabled Check", CheckTypes.Http, "60", false);

        var recentTime = DateTimeOffset.UtcNow.AddSeconds(-1);
        await CreateCheckResultAsync(enabledCheck.Id, CheckStatuses.Up, recentTime);
        await CreateCheckResultAsync(disabledCheck.Id, CheckStatuses.Down, recentTime);

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 1);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);

        var lastIncrement = result.Increments.Last();
        Assert.AreEqual(1, lastIncrement.TotalChecks);
        Assert.AreEqual(1, lastIncrement.UpCount);
        Assert.AreEqual(0, lastIncrement.DownCount);
    }

    [TestMethod]
    public async Task GetWorkspaceIncidentTimelineAsyncShouldHandleChecksWithoutResults()
    {
        var checkWithResults = await CreateCheckAsync("Check With Results", CheckTypes.Http, "60", true);
        var checkWithoutResults = await CreateCheckAsync("Check Without Results", CheckTypes.Http, "60", true);

        var recentTime = DateTimeOffset.UtcNow.AddSeconds(-1);
        await CreateCheckResultAsync(checkWithResults.Id, CheckStatuses.Up, recentTime);

        var result = await _service.GetWorkspaceIncidentTimelineAsync(_workspace.Id, 1);

        Assert.IsNotNull(result);
        Assert.IsNotEmpty(result.Increments);

        var lastIncrement = result.Increments.Last();
        Assert.AreEqual(2, lastIncrement.TotalChecks);
        Assert.AreEqual(1, lastIncrement.UpCount);
    }

    private async Task<Workspace> CreateWorkspaceAsync(string name)
    {
        var workspace = new Workspace
        {
            Name = name,
            IsPublic = false,
            CreatedAt = _testStartTime,
            UpdatedAt = _testStartTime
        };

        DbContext.Workspaces.Add(workspace);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return workspace;
    }

    private async Task<Check> CreateCheckAsync(
        string name,
        string checkType,
        string schedule,
        bool enabled,
        string? description = null,
        Dictionary<string, System.Text.Json.JsonElement>? configJson = null,
        Guid? workspaceId = null)
    {
        var check = new Check
        {
            WorkspaceId = workspaceId ?? _workspace.Id,
            Name = name,
            Description = description,
            CheckType = checkType,
            ConfigurationJson = configJson ?? [],
            Schedule = schedule,
            TimeoutSeconds = 30,
            Enabled = enabled,
            CreatedAt = _testStartTime,
            UpdatedAt = _testStartTime
        };

        DbContext.Checks.Add(check);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return check;
    }

    private async Task<CheckResult> CreateCheckResultAsync(
        Guid checkId,
        string status,
        DateTimeOffset checkedAt,
        string? errorMessage = null,
        int? responseTimeMs = null)
    {
        var result = new CheckResult
        {
            CheckId = checkId,
            Status = status,
            CheckedAt = checkedAt,
            ErrorMessage = errorMessage,
            ResponseTimeMs = responseTimeMs
        };

        DbContext.CheckResults.Add(result);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return result;
    }

    private async Task<Alert> CreateAlertAsync(Guid checkId, string name)
    {
        var alert = new Alert
        {
            CheckId = checkId,
            Name = name,
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

        return alert;
    }

    private async Task<Alert> CreateDetailedAlertAsync(
        Guid checkId,
        string name,
        bool triggerOnWarn,
        bool triggerOnDown,
        int failureThreshold,
        bool sendRecoveryNotification,
        bool enabled,
        int channelCount)
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

        for (int i = 0; i < channelCount; i++)
        {
            var channel = new NotificationChannel
            {
                WorkspaceId = _workspace.Id,
                Name = $"Channel {i + 1}",
                ChannelType = "Email",
                ConfigurationJson = [],
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            DbContext.NotificationChannels.Add(channel);

            alert.NotificationChannels.Add(channel);
        }

        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        return alert;
    }
}
