using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Checks;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Checks;

[TestClass]
public class IndexModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private CheckQueryService _mockCheckQuery = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private IndexModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>((SamaDbContext)null!);
        _mockCheckQuery = Substitute.For<CheckQueryService>(null!, null!, null!, null!);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!, null!, null!);

        _pageModel = new IndexModel(_mockWorkspaceQuery, _mockCheckQuery, _mockGlobalSettings);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenWorkspaceExists()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<CheckListItemViewModel>()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadChecksFromQueryService()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        var expectedChecks = new List<CheckListItemViewModel>
        {
            new() { Id = Guid.NewGuid(), Name = "Check 1" },
            new() { Id = Guid.NewGuid(), Name = "Check 2" }
        };
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedChecks));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);

        await _pageModel.OnGetAsync(workspaceId);

        Assert.HasCount(2, _pageModel.Checks);
        Assert.AreEqual("Check 1", _pageModel.Checks[0].Name);
        Assert.AreEqual("Check 2", _pageModel.Checks[1].Name);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetRefreshIntervalFromGlobalSettings()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<CheckListItemViewModel>()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(999);

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(999, _pageModel.RefreshIntervalSeconds);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnRedirectWhenWorkspaceIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Workspaces/Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetWorkspaceIdProperty()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<CheckListItemViewModel>()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(workspaceId, _pageModel.WorkspaceId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetWorkspaceNameProperty()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "My Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetChecksForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<CheckListItemViewModel>()));
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(5);

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual("My Workspace", _pageModel.WorkspaceName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldNotLoadChecksWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        await _pageModel.OnGetAsync(workspaceId);

        await _mockCheckQuery.DidNotReceive().GetChecksForWorkspaceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
