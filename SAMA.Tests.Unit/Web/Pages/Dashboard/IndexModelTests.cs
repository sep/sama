using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Dashboard;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Dashboard;

[TestClass]
public class IndexModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private CheckQueryService _mockCheckQuery = null!;
    private AlertQueryService _mockAlertQuery = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private MarkdownService _markdownService = null!;
    private IndexModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>((SamaDbContext)null!);
        _mockCheckQuery = Substitute.For<CheckQueryService>(null!, null!, null!, null!);
        _mockAlertQuery = Substitute.For<AlertQueryService>((SamaDbContext)null!);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null, null);
        _markdownService = new MarkdownService();

        _pageModel = new IndexModel(_mockWorkspaceQuery, _mockCheckQuery, _mockAlertQuery, _mockGlobalSettings, _markdownService);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetAsync(workspaceId, null, null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenWorkspaceExists()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checks = new List<CheckListItemViewModel>();
        var alerts = new List<RecentAlertViewModel>();

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId).Returns(Task.FromResult(checks));
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, Arg.Any<int>()).Returns(Task.FromResult(alerts));
        _mockCheckQuery.GetWorkspaceIncidentTimelineAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceIncidentTimelineViewModel()));
        _mockCheckQuery.GetWorkspaceResponseTimeTrendsAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceResponseTimeTrendsViewModel()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        _mockGlobalSettings.MaxRecentAlerts.Returns(20);

        var result = await _pageModel.OnGetAsync(workspaceId, null, null);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateRefreshIntervalFromGlobalSettings()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checks = new List<CheckListItemViewModel>();
        var alerts = new List<RecentAlertViewModel>();

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId).Returns(Task.FromResult(checks));
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, Arg.Any<int>()).Returns(Task.FromResult(alerts));
        _mockCheckQuery.GetWorkspaceIncidentTimelineAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceIncidentTimelineViewModel()));
        _mockCheckQuery.GetWorkspaceResponseTimeTrendsAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceResponseTimeTrendsViewModel()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(10);
        _mockGlobalSettings.MaxRecentAlerts.Returns(20);

        await _pageModel.OnGetAsync(workspaceId, null, null);

        Assert.AreEqual(10, _pageModel.RefreshIntervalSeconds);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateChecksList()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checks = new List<CheckListItemViewModel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Check 1",
                CheckType = "HTTP",
                Enabled = true,
                Schedule = "60",
                LastStatus = "Up",
                LastCheckedAt = DateTimeOffset.UtcNow,
                LastResponseTimeMs = 100,
                LastErrorMessage = null,
                AlertCount = 1
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Check 2",
                CheckType = "TCP",
                Enabled = false,
                Schedule = "120",
                LastStatus = null,
                LastCheckedAt = null,
                LastResponseTimeMs = null,
                LastErrorMessage = null,
                AlertCount = 0
            }
        };
        var alerts = new List<RecentAlertViewModel>();

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId).Returns(Task.FromResult(checks));
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, Arg.Any<int>()).Returns(Task.FromResult(alerts));
        _mockCheckQuery.GetWorkspaceIncidentTimelineAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceIncidentTimelineViewModel()));
        _mockCheckQuery.GetWorkspaceResponseTimeTrendsAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceResponseTimeTrendsViewModel()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        _mockGlobalSettings.MaxRecentAlerts.Returns(20);

        await _pageModel.OnGetAsync(workspaceId, null, null);

        Assert.HasCount(2, _pageModel.Checks);
        Assert.AreEqual("Check 1", _pageModel.Checks[0].Name);
        Assert.AreEqual("Check 2", _pageModel.Checks[1].Name);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateRecentAlertsList()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checks = new List<CheckListItemViewModel>();
        var alerts = new List<RecentAlertViewModel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CheckName = "Test Check",
                CheckId = Guid.NewGuid(),
                AlertName = "Test Alert",
                Status = "Down",
                SentAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            },
            new()
            {
                Id = Guid.NewGuid(),
                CheckName = "Other Check",
                CheckId = Guid.NewGuid(),
                AlertName = "Critical Alert",
                Status = "Up",
                SentAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            }
        };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId).Returns(Task.FromResult(checks));
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, Arg.Any<int>()).Returns(Task.FromResult(alerts));
        _mockCheckQuery.GetWorkspaceIncidentTimelineAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceIncidentTimelineViewModel()));
        _mockCheckQuery.GetWorkspaceResponseTimeTrendsAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceResponseTimeTrendsViewModel()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        _mockGlobalSettings.MaxRecentAlerts.Returns(20);

        await _pageModel.OnGetAsync(workspaceId, null, null);

        Assert.HasCount(2, _pageModel.RecentAlerts);
        Assert.AreEqual("Test Check", _pageModel.RecentAlerts[0].CheckName);
        Assert.AreEqual("Other Check", _pageModel.RecentAlerts[1].CheckName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateEmptyListsWhenNoData()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checks = new List<CheckListItemViewModel>();
        var alerts = new List<RecentAlertViewModel>();

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId).Returns(Task.FromResult(checks));
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, Arg.Any<int>()).Returns(Task.FromResult(alerts));
        _mockCheckQuery.GetWorkspaceIncidentTimelineAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceIncidentTimelineViewModel()));
        _mockCheckQuery.GetWorkspaceResponseTimeTrendsAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceResponseTimeTrendsViewModel()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        _mockGlobalSettings.MaxRecentAlerts.Returns(20);

        await _pageModel.OnGetAsync(workspaceId, null, null);

        Assert.IsEmpty(_pageModel.Checks);
        Assert.IsEmpty(_pageModel.RecentAlerts);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetChecksForWorkspaceAsyncWithCorrectId()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checks = new List<CheckListItemViewModel>();
        var alerts = new List<RecentAlertViewModel>();

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId).Returns(Task.FromResult(checks));
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, Arg.Any<int>()).Returns(Task.FromResult(alerts));
        _mockCheckQuery.GetWorkspaceIncidentTimelineAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceIncidentTimelineViewModel()));
        _mockCheckQuery.GetWorkspaceResponseTimeTrendsAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceResponseTimeTrendsViewModel()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        _mockGlobalSettings.MaxRecentAlerts.Returns(20);

        await _pageModel.OnGetAsync(workspaceId, null, null);

        await _mockCheckQuery.Received(1).GetChecksForWorkspaceAsync(workspaceId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetRecentAlertsForWorkspaceAsyncWithCorrectParameters()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checks = new List<CheckListItemViewModel>();
        var alerts = new List<RecentAlertViewModel>();

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId).Returns(Task.FromResult(checks));
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, 50).Returns(Task.FromResult(alerts));
        _mockCheckQuery.GetWorkspaceIncidentTimelineAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceIncidentTimelineViewModel()));
        _mockCheckQuery.GetWorkspaceResponseTimeTrendsAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceResponseTimeTrendsViewModel()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        _mockGlobalSettings.MaxRecentAlerts.Returns(50);

        await _pageModel.OnGetAsync(workspaceId, null, null);

        await _mockAlertQuery.Received(1).GetRecentAlertsForWorkspaceAsync(workspaceId, 50);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldNotCallQueryServicesWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        await _pageModel.OnGetAsync(workspaceId, null, null);

        await _mockCheckQuery.DidNotReceive().GetChecksForWorkspaceAsync(Arg.Any<Guid>());
        await _mockAlertQuery.DidNotReceive().GetRecentAlertsForWorkspaceAsync(Arg.Any<Guid>(), Arg.Any<int>());
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadWorkspaceContextWithCorrectParameters()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "My Workspace" };
        var checks = new List<CheckListItemViewModel>();
        var alerts = new List<RecentAlertViewModel>();

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId).Returns(Task.FromResult(checks));
        _mockAlertQuery.GetRecentAlertsForWorkspaceAsync(workspaceId, Arg.Any<int>()).Returns(Task.FromResult(alerts));
        _mockCheckQuery.GetWorkspaceIncidentTimelineAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceIncidentTimelineViewModel()));
        _mockCheckQuery.GetWorkspaceResponseTimeTrendsAsync(workspaceId, Arg.Any<int>(), Arg.Any<int>()).Returns(Task.FromResult(new WorkspaceResponseTimeTrendsViewModel()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        _mockGlobalSettings.MaxRecentAlerts.Returns(20);

        await _pageModel.OnGetAsync(workspaceId, null, null);

        await _mockWorkspaceQuery.Received(1).GetWorkspaceByIdAsync(workspaceId);
        Assert.AreEqual(workspaceId, _pageModel.WorkspaceId);
        Assert.AreEqual("My Workspace", _pageModel.WorkspaceName);
    }
}
