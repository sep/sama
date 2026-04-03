using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.NotificationChannels;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.NotificationChannels;

[TestClass]
public class DeleteModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private ChannelQueryService _mockChannelQuery = null!;
    private ChannelCommandService _mockChannelCommand = null!;
    private DeleteModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockChannelQuery = Substitute.For<ChannelQueryService>(null!, null!);
        _mockChannelCommand = Substitute.For<ChannelCommandService>(null!, null!);

        _pageModel = new DeleteModel(_mockWorkspaceQuery, _mockChannelQuery, _mockChannelCommand);
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
    public async Task OnGetAsyncShouldPopulateChannelToDelete()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId, "Channel To Delete");
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(channelId);

        Assert.AreEqual(channelId, _pageModel.ChannelToDelete.Id);
        Assert.AreEqual("Channel To Delete", _pageModel.ChannelToDelete.Name);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnPostAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenChannelDoesNotExist()
    {
        var channelId = Guid.NewGuid();
        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(null));

        var result = await _pageModel.OnPostAsync(channelId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldDeleteChannelAndRedirect()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId, "Channel To Delete");

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockChannelCommand.DeleteChannelAsync(channelId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var result = await _pageModel.OnPostAsync(channelId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.AreEqual(workspaceId, redirect.RouteValues!["workspaceId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallDeleteChannelAsync()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockChannelCommand.DeleteChannelAsync(channelId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(channelId);

        await _mockChannelCommand.Received(1).DeleteChannelAsync(
            channelId,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallDeleteWhenChannelDoesNotExist()
    {
        var channelId = Guid.NewGuid();
        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(null));

        await _pageModel.OnPostAsync(channelId);

        await _mockChannelCommand.DidNotReceive().DeleteChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static ChannelDetailsViewModel CreateTestChannel(
        Guid id,
        Guid workspaceId,
        string name = "Test Channel")
    {
        return new ChannelDetailsViewModel
        {
            Id = id,
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace",
            Name = name,
            ChannelType = "Email",
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
