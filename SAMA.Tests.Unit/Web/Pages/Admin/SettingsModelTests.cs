using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data;
using SAMA.Data.Services;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Pages.Admin;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Admin;

[TestClass]
public class SettingsModelTests
{
    private GlobalSettingsService _mockGlobalSettings = null!;
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private ConfigurationExportService _mockExportService = null!;
    private ConfigurationImportService _mockImportService = null!;
    private ILogger<SettingsModel> _mockLogger = null!;
    private SettingsModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!);
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>((SamaDbContext)null!);
        _mockExportService = Substitute.For<ConfigurationExportService>(null!, null!, null!);
        _mockImportService = Substitute.For<ConfigurationImportService>(null!, null!, null!);
        _mockLogger = Substitute.For<ILogger<SettingsModel>>();

        _mockWorkspaceQuery.GetWorkspacesAsync(Arg.Any<List<Guid>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.WorkspaceDetailsViewModel>()));

        _pageModel = new SettingsModel(_mockGlobalSettings, _mockWorkspaceQuery, _mockExportService, _mockImportService, _mockLogger);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetShouldLoadCurrentSettings()
    {
        var workspaceId = Guid.NewGuid();
        _mockGlobalSettings.CheckResultsRetentionDays.Returns(180);
        _mockGlobalSettings.AlertHistoryRetentionDays.Returns(90);
        _mockGlobalSettings.AuditLogRetentionDays.Returns(730);
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(10);
        _mockGlobalSettings.MaxRecentAlerts.Returns(50);
        _mockGlobalSettings.DefaultCheckTimeoutSeconds.Returns(60);
        _mockGlobalSettings.NotificationTimeoutSeconds.Returns(45);
        _mockGlobalSettings.AnonymousDefaultWorkspaceId.Returns(workspaceId);

        await _pageModel.OnGetAsync();

        Assert.AreEqual(180, _pageModel.Input.CheckResultsRetentionDays);
        Assert.AreEqual(90, _pageModel.Input.AlertHistoryRetentionDays);
        Assert.AreEqual(730, _pageModel.Input.AuditLogRetentionDays);
        Assert.AreEqual(10, _pageModel.Input.DashboardRefreshIntervalSeconds);
        Assert.AreEqual(50, _pageModel.Input.MaxRecentAlerts);
        Assert.AreEqual(60, _pageModel.Input.DefaultCheckTimeoutSeconds);
        Assert.AreEqual(45, _pageModel.Input.NotificationTimeoutSeconds);
        Assert.AreEqual(workspaceId, _pageModel.Input.AnonymousDefaultWorkspaceId);
    }

    [TestMethod]
    public async Task OnPostShouldUpdateAllSettingsWhenModelStateIsValid()
    {
        var workspaceId = Guid.NewGuid();
        _pageModel.Input = new SettingsModel.InputModel
        {
            CheckResultsRetentionDays = 180,
            AlertHistoryRetentionDays = 90,
            AuditLogRetentionDays = 730,
            DashboardRefreshIntervalSeconds = 10,
            MaxRecentAlerts = 50,
            DefaultCheckTimeoutSeconds = 60,
            NotificationTimeoutSeconds = 45,
            AnonymousDefaultWorkspaceId = workspaceId
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        _mockGlobalSettings.Received(1).CheckResultsRetentionDays = 180;
        _mockGlobalSettings.Received(1).AlertHistoryRetentionDays = 90;
        _mockGlobalSettings.Received(1).AuditLogRetentionDays = 730;
        _mockGlobalSettings.Received(1).DashboardRefreshIntervalSeconds = 10;
        _mockGlobalSettings.Received(1).MaxRecentAlerts = 50;
        _mockGlobalSettings.Received(1).DefaultCheckTimeoutSeconds = 60;
        _mockGlobalSettings.Received(1).NotificationTimeoutSeconds = 45;
        _mockGlobalSettings.Received(1).AnonymousDefaultWorkspaceId = workspaceId;
    }

    [TestMethod]
    public async Task OnPostShouldSetSuccessMessageWhenSaveSucceeds()
    {
        _pageModel.Input = new SettingsModel.InputModel
        {
            CheckResultsRetentionDays = 365,
            AlertHistoryRetentionDays = 365,
            AuditLogRetentionDays = 365,
            DashboardRefreshIntervalSeconds = 5,
            MaxRecentAlerts = 20,
            DefaultCheckTimeoutSeconds = 30,
            NotificationTimeoutSeconds = 30
        };

        await _pageModel.OnPostAsync();

        Assert.AreEqual("Settings updated successfully.", _pageModel.TempData["SuccessMessage"]);
    }

    [TestMethod]
    public async Task OnPostShouldReturnPageWhenModelStateIsInvalid()
    {
        _pageModel.ModelState.AddModelError("Input.CheckResultsRetentionDays", "Required");

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        _mockGlobalSettings.DidNotReceive().CheckResultsRetentionDays = Arg.Any<int>();
    }

    [TestMethod]
    public async Task OnPostShouldAddModelErrorWhenExceptionOccurs()
    {
        _pageModel.Input = new SettingsModel.InputModel
        {
            CheckResultsRetentionDays = 365,
            AlertHistoryRetentionDays = 365,
            AuditLogRetentionDays = 365,
            DashboardRefreshIntervalSeconds = 5,
            MaxRecentAlerts = 20,
            DefaultCheckTimeoutSeconds = 30,
            NotificationTimeoutSeconds = 30
        };

        _mockGlobalSettings.When(x => x.CheckResultsRetentionDays = Arg.Any<int>())
            .Do(_ => throw new Exception("Database error"));

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsGreaterThan(0, _pageModel.ModelState.ErrorCount);
        Assert.IsNull(_pageModel.TempData["SuccessMessage"]);
    }

    [TestMethod]
    public async Task OnPostShouldHandleMinimumValidValues()
    {
        _pageModel.Input = new SettingsModel.InputModel
        {
            CheckResultsRetentionDays = 30,
            AlertHistoryRetentionDays = 30,
            AuditLogRetentionDays = 30,
            DashboardRefreshIntervalSeconds = 1,
            MaxRecentAlerts = 10,
            DefaultCheckTimeoutSeconds = 5,
            NotificationTimeoutSeconds = 5
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        _mockGlobalSettings.Received(1).CheckResultsRetentionDays = 30;
        _mockGlobalSettings.Received(1).DashboardRefreshIntervalSeconds = 1;
    }

    [TestMethod]
    public async Task OnPostShouldHandleMaximumValidValues()
    {
        _pageModel.Input = new SettingsModel.InputModel
        {
            CheckResultsRetentionDays = 3650,
            AlertHistoryRetentionDays = 3650,
            AuditLogRetentionDays = 3650,
            DashboardRefreshIntervalSeconds = 300,
            MaxRecentAlerts = 500,
            DefaultCheckTimeoutSeconds = 300,
            NotificationTimeoutSeconds = 300
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        _mockGlobalSettings.Received(1).CheckResultsRetentionDays = 3650;
        _mockGlobalSettings.Received(1).DashboardRefreshIntervalSeconds = 300;
    }

    [TestMethod]
    public async Task OnPostShouldSaveAnonymousDefaultWorkspaceIdAsNull()
    {
        _pageModel.Input = new SettingsModel.InputModel
        {
            CheckResultsRetentionDays = 365,
            AlertHistoryRetentionDays = 365,
            AuditLogRetentionDays = 365,
            DashboardRefreshIntervalSeconds = 5,
            MaxRecentAlerts = 20,
            DefaultCheckTimeoutSeconds = 30,
            NotificationTimeoutSeconds = 30,
            AnonymousDefaultWorkspaceId = null
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        _mockGlobalSettings.Received(1).AnonymousDefaultWorkspaceId = null;
    }
}
