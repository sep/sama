using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.NotificationChannels;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.NotificationChannels;

[TestClass]
public class IndexModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private ChannelQueryService _mockChannelQuery = null!;
    private IndexModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockChannelQuery = Substitute.For<ChannelQueryService>(null!, null!);

        _pageModel = new IndexModel(_mockWorkspaceQuery, _mockChannelQuery);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenWorkspaceExists()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ChannelListItemViewModel>()));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadChannelsFromQueryService()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        var expectedChannels = new List<ChannelListItemViewModel>
        {
            new() { Id = Guid.NewGuid(), Name = "Channel 1", ChannelType = "Email", Enabled = true },
            new() { Id = Guid.NewGuid(), Name = "Channel 2", ChannelType = "Slack", Enabled = false }
        };
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedChannels));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.HasCount(2, _pageModel.NotificationChannels);
        Assert.AreEqual("Channel 1", _pageModel.NotificationChannels[0].Name);
        Assert.AreEqual("Channel 2", _pageModel.NotificationChannels[1].Name);
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
    public async Task OnGetAsyncShouldSetWorkspaceProperties()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "My Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ChannelListItemViewModel>()));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(workspaceId, _pageModel.WorkspaceId);
        Assert.AreEqual("My Workspace", _pageModel.WorkspaceName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldNotLoadChannelsWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        await _pageModel.OnGetAsync(workspaceId);

        await _mockChannelQuery.DidNotReceive().GetChannelsForWorkspaceAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
