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
public class DetailsModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private ChannelQueryService _mockChannelQuery = null!;
    private DetailsModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockChannelQuery = Substitute.For<ChannelQueryService>(null!, null!);

        _pageModel = new DetailsModel(_mockWorkspaceQuery, _mockChannelQuery);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenChannelDoesNotExist()
    {
        var channelId = Guid.NewGuid();
        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(null));

        var result = await _pageModel.OnGetAsync(channelId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenChannelExists()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(channelId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateChannelDetails()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId, "Email Channel", "Email");
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(channelId);

        Assert.AreEqual(channelId, _pageModel.Channel.Id);
        Assert.AreEqual("Email Channel", _pageModel.Channel.Name);
        Assert.AreEqual("Email", _pageModel.Channel.ChannelType);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetWorkspaceProperties()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);
        var workspace = new Workspace { Id = workspaceId, Name = "My Workspace" };

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(channelId);

        Assert.AreEqual(workspaceId, _pageModel.WorkspaceId);
        Assert.AreEqual("My Workspace", _pageModel.WorkspaceName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldNotLoadWorkspaceWhenChannelDoesNotExist()
    {
        var channelId = Guid.NewGuid();
        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(null));

        await _pageModel.OnGetAsync(channelId);

        await _mockWorkspaceQuery.DidNotReceive().GetWorkspaceByIdAsync(Arg.Any<Guid>());
    }

    private static ChannelDetailsViewModel CreateTestChannel(
        Guid id,
        Guid workspaceId,
        string name = "Test Channel",
        string channelType = "Email")
    {
        return new ChannelDetailsViewModel
        {
            Id = id,
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace",
            Name = name,
            ChannelType = channelType,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            AlertCount = 0,
            EventSubscriptionCount = 0,
            MaskedConfiguration = [],
            ConfigurationJson = []
        };
    }
}
