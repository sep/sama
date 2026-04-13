using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Dashboard;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;
using static SAMA.Web.Services.DashboardCacheService;

namespace SAMA.Tests.Unit.Web.Pages.Dashboard;

[TestClass]
public class IndexModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private MarkdownService _markdownService = null!;
    private DashboardCacheService _cacheService = null!;
    private IndexModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null, null, null, null);
        _markdownService = new MarkdownService();
        _cacheService = new DashboardCacheService(new ServiceCollection().BuildServiceProvider());

        _pageModel = new IndexModel(_mockWorkspaceQuery, _mockGlobalSettings, _markdownService, _cacheService);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);

        _mockWorkspaceQuery.GetDashboardMessageAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
    }

    private void PrePopulateCache(Guid workspaceId, List<CheckListItemViewModel>? checks = null, List<RecentAlertViewModel>? alerts = null)
    {
        _cacheService.SetWorkspaceData(workspaceId, new WorkspaceDashboardData(
            checks ?? [],
            alerts ?? []
        ));
        _cacheService.SetTimeline(workspaceId, 24, new WorkspaceIncidentTimelineViewModel());
        _cacheService.SetTrends(workspaceId, 24, new WorkspaceResponseTimeTrendsViewModel());
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
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        PrePopulateCache(workspaceId);

        var result = await _pageModel.OnGetAsync(workspaceId, null, null);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateRefreshIntervalFromGlobalSettings()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(10);
        PrePopulateCache(workspaceId);

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
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        PrePopulateCache(workspaceId, checks: checks);

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
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        PrePopulateCache(workspaceId, alerts: alerts);

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
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        PrePopulateCache(workspaceId);

        await _pageModel.OnGetAsync(workspaceId, null, null);

        Assert.IsEmpty(_pageModel.Checks);
        Assert.IsEmpty(_pageModel.RecentAlerts);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadWorkspaceContextWithCorrectParameters()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "My Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);
        PrePopulateCache(workspaceId);

        await _pageModel.OnGetAsync(workspaceId, null, null);

        await _mockWorkspaceQuery.Received(1).GetWorkspaceByIdAsync(workspaceId);
        Assert.AreEqual(workspaceId, _pageModel.WorkspaceId);
        Assert.AreEqual("My Workspace", _pageModel.WorkspaceName);
    }

    [TestMethod]
    public async Task OnGetTimelineAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetTimelineAsync(workspaceId, null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetTimelineAsyncShouldReturnOobContentWithTimelineData()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _cacheService.SetTimeline(workspaceId, 6, new WorkspaceIncidentTimelineViewModel { Hours = 6 });

        var result = await _pageModel.OnGetTimelineAsync(workspaceId, 6);

        Assert.IsInstanceOfType<ContentResult>(result);
        var content = (ContentResult)result;
        Assert.AreEqual("text/html", content.ContentType);
        Assert.IsTrue(content.Content!.Contains("incidentTimelineChartData"));
        Assert.IsTrue(content.Content!.Contains("hx-swap-oob"));
        Assert.IsTrue(content.Content!.Contains("\"hours\":6"));
    }

    [TestMethod]
    public async Task OnGetTimelineAsyncShouldDefaultTo24Hours()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _cacheService.SetTimeline(workspaceId, 24, new WorkspaceIncidentTimelineViewModel { Hours = 24 });

        var result = await _pageModel.OnGetTimelineAsync(workspaceId, null);

        Assert.IsInstanceOfType<ContentResult>(result);
        var content = (ContentResult)result;
        Assert.IsTrue(content.Content!.Contains("\"hours\":24"));
    }

    [TestMethod]
    public async Task OnGetTrendsAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetTrendsAsync(workspaceId, null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetTrendsAsyncShouldReturnOobContentWithTrendsData()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _cacheService.SetTrends(workspaceId, 3, new WorkspaceResponseTimeTrendsViewModel { Hours = 3 });

        var result = await _pageModel.OnGetTrendsAsync(workspaceId, 3);

        Assert.IsInstanceOfType<ContentResult>(result);
        var content = (ContentResult)result;
        Assert.AreEqual("text/html", content.ContentType);
        Assert.IsTrue(content.Content!.Contains("responseTimeTrendsChartData"));
        Assert.IsTrue(content.Content!.Contains("hx-swap-oob"));
        Assert.IsTrue(content.Content!.Contains("\"hours\":3"));
    }

    [TestMethod]
    public async Task OnGetTrendsAsyncShouldDefaultTo24Hours()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _cacheService.SetTrends(workspaceId, 24, new WorkspaceResponseTimeTrendsViewModel { Hours = 24 });

        var result = await _pageModel.OnGetTrendsAsync(workspaceId, null);

        Assert.IsInstanceOfType<ContentResult>(result);
        var content = (ContentResult)result;
        Assert.IsTrue(content.Content!.Contains("\"hours\":24"));
    }
}
