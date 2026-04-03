using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Alerts;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Alerts;

[TestClass]
public class IndexModelTests
{
    private CheckQueryService _mockCheckQuery = null!;
    private AlertQueryService _mockAlertQuery = null!;
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private IndexModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockCheckQuery = Substitute.For<CheckQueryService>(null!, null!, null!, null!);
        _mockAlertQuery = Substitute.For<AlertQueryService>((SamaDbContext)null!);
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);

        _pageModel = new IndexModel(_mockWorkspaceQuery, _mockCheckQuery, _mockAlertQuery);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnRedirectWhenCheckIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Workspaces/Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenCheckDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(null));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenCheckExists()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var alerts = new List<AlertListItemViewModel>();

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertQuery.GetAlertsForCheckAsync(checkId).Returns(Task.FromResult(alerts));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateCheckIdAndName()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "My Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var alerts = new List<AlertListItemViewModel>();

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertQuery.GetAlertsForCheckAsync(checkId).Returns(Task.FromResult(alerts));

        await _pageModel.OnGetAsync(checkId);

        Assert.AreEqual(checkId, _pageModel.CheckId);
        Assert.AreEqual("My Test Check", _pageModel.CheckName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateAlertsList()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var alerts = new List<AlertListItemViewModel>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Alert 1",
                TriggerOnWarn = true,
                TriggerOnDown = false,
                FailureThreshold = 2,
                SendRecoveryNotification = true,
                Enabled = true,
                ChannelCount = 1,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Alert 2",
                TriggerOnWarn = false,
                TriggerOnDown = true,
                FailureThreshold = 3,
                SendRecoveryNotification = false,
                Enabled = false,
                ChannelCount = 2,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertQuery.GetAlertsForCheckAsync(checkId).Returns(Task.FromResult(alerts));

        await _pageModel.OnGetAsync(checkId);

        Assert.HasCount(2, _pageModel.Alerts);
        Assert.AreEqual("Alert 1", _pageModel.Alerts[0].Name);
        Assert.AreEqual("Alert 2", _pageModel.Alerts[1].Name);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateEmptyAlertsListWhenNoAlerts()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var alerts = new List<AlertListItemViewModel>();

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertQuery.GetAlertsForCheckAsync(checkId).Returns(Task.FromResult(alerts));

        await _pageModel.OnGetAsync(checkId);

        Assert.IsEmpty(_pageModel.Alerts);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetCheckBasicInfoAsyncWithCorrectId()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var alerts = new List<AlertListItemViewModel>();

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertQuery.GetAlertsForCheckAsync(checkId).Returns(Task.FromResult(alerts));

        await _pageModel.OnGetAsync(checkId);

        await _mockCheckQuery.Received(1).GetCheckBasicInfoAsync(checkId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetAlertsForCheckAsyncWithCorrectId()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var alerts = new List<AlertListItemViewModel>();

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertQuery.GetAlertsForCheckAsync(checkId).Returns(Task.FromResult(alerts));

        await _pageModel.OnGetAsync(checkId);

        await _mockAlertQuery.Received(1).GetAlertsForCheckAsync(checkId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldNotCallGetAlertsWhenCheckDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(null));

        await _pageModel.OnGetAsync(checkId);

        await _mockAlertQuery.DidNotReceive().GetAlertsForCheckAsync(Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadWorkspaceContextWithCorrectParameters()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var alerts = new List<AlertListItemViewModel>();

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertQuery.GetAlertsForCheckAsync(checkId).Returns(Task.FromResult(alerts));

        await _pageModel.OnGetAsync(checkId);

        await _mockWorkspaceQuery.Received(1).GetWorkspaceByIdAsync(workspaceId);
        Assert.AreEqual(workspaceId, _pageModel.WorkspaceId);
        Assert.AreEqual("Test Workspace", _pageModel.WorkspaceName);
    }
}
