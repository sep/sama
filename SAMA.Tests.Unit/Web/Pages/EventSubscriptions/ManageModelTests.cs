using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Pages.EventSubscriptions;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;
using static SAMA.Web.Services.Commands.EventSubscriptionCommandService;

namespace SAMA.Tests.Unit.Web.Pages.EventSubscriptions;

[TestClass]
public class ManageModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private EventSubscriptionQueryService _mockEventSubscriptionQuery = null!;
    private EventSubscriptionCommandService _mockEventSubscriptionCommand = null!;
    private ManageModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockEventSubscriptionQuery = Substitute.For<EventSubscriptionQueryService>((SamaDbContext)null!);
        _mockEventSubscriptionCommand = Substitute.For<EventSubscriptionCommandService>(null!, null!);

        _pageModel = new ManageModel(
            _mockWorkspaceQuery,
            _mockEventSubscriptionQuery,
            _mockEventSubscriptionCommand);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnRedirectWhenWorkspaceIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null, EventTypes.CheckCreated);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetAsync(workspaceId, EventTypes.CheckCreated);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnRedirectWhenEventTypeIsNull()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(workspaceId, null);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnRedirectWhenEventTypeIsInvalid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(workspaceId, "InvalidEventType");

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageForValidEventType()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetChannelsForEventTypeAsync(workspaceId, EventTypes.CheckCreated, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionChannelViewModel>()));

        var result = await _pageModel.OnGetAsync(workspaceId, EventTypes.CheckCreated);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetEventTypeInInput()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetChannelsForEventTypeAsync(workspaceId, EventTypes.CheckUpdated, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionChannelViewModel>()));

        await _pageModel.OnGetAsync(workspaceId, EventTypes.CheckUpdated);

        Assert.AreEqual(EventTypes.CheckUpdated, _pageModel.Input.EventType);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadChannelsFromQueryService()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var expectedChannels = new List<EventSubscriptionChannelViewModel>
        {
            new() { Id = Guid.NewGuid(), Name = "Channel 1", ChannelType = "Email", Enabled = true, IsSubscribed = true },
            new() { Id = Guid.NewGuid(), Name = "Channel 2", ChannelType = "Slack", Enabled = true, IsSubscribed = false }
        };
        _mockEventSubscriptionQuery.GetChannelsForEventTypeAsync(workspaceId, EventTypes.CheckCreated, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedChannels));

        await _pageModel.OnGetAsync(workspaceId, EventTypes.CheckCreated);

        Assert.HasCount(2, _pageModel.Channels);
        Assert.AreEqual("Channel 1", _pageModel.Channels[0].Name);
        Assert.AreEqual("Channel 2", _pageModel.Channels[1].Name);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateSelectedChannelIds()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var channel1Id = Guid.NewGuid();
        var channel2Id = Guid.NewGuid();
        var expectedChannels = new List<EventSubscriptionChannelViewModel>
        {
            new() { Id = channel1Id, Name = "Channel 1", ChannelType = "Email", Enabled = true, IsSubscribed = true },
            new() { Id = channel2Id, Name = "Channel 2", ChannelType = "Slack", Enabled = true, IsSubscribed = false }
        };
        _mockEventSubscriptionQuery.GetChannelsForEventTypeAsync(workspaceId, EventTypes.CheckCreated, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedChannels));

        await _pageModel.OnGetAsync(workspaceId, EventTypes.CheckCreated);

        Assert.HasCount(1, _pageModel.Input.SelectedChannelIds);
        Assert.AreEqual(channel1Id, _pageModel.Input.SelectedChannelIds[0]);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldAcceptAllValidEventTypes()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetChannelsForEventTypeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionChannelViewModel>()));

        var validEventTypes = new[]
        {
            EventTypes.CheckCreated,
            EventTypes.CheckUpdated,
            EventTypes.CheckDeleted,
            EventTypes.CheckStatusChanged
        };

        foreach (var eventType in validEventTypes)
        {
            var result = await _pageModel.OnGetAsync(workspaceId, eventType);
            Assert.IsInstanceOfType<PageResult>(result, $"Failed for event type: {eventType}");
        }
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnRedirectWhenWorkspaceIdIsNull()
    {
        var result = await _pageModel.OnPostAsync(null);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnPostAsync(workspaceId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenModelStateIsInvalid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetChannelsForEventTypeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionChannelViewModel>()));

        _pageModel.ModelState.AddModelError("Input.EventType", "Required");
        _pageModel.Input.EventType = EventTypes.CheckCreated;

        var result = await _pageModel.OnPostAsync(workspaceId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallCommandServiceWithCorrectParameters()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var channel1Id = Guid.NewGuid();
        var channel2Id = Guid.NewGuid();
        _pageModel.Input = new ManageModel.InputModel
        {
            EventType = EventTypes.CheckCreated,
            SelectedChannelIds = [channel1Id, channel2Id]
        };

        _mockEventSubscriptionCommand.UpdateEventSubscriptionsAsync(
            workspaceId,
            EventTypes.CheckCreated,
            Arg.Any<List<Guid>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UpdateEventSubscriptionsResult
            {
                Success = true,
                CreatedCount = 2,
                DeletedCount = 0
            }));

        await _pageModel.OnPostAsync(workspaceId);

        await _mockEventSubscriptionCommand.Received(1).UpdateEventSubscriptionsAsync(
            workspaceId,
            EventTypes.CheckCreated,
            Arg.Is<List<Guid>>(ids => ids.Count == 2 && ids.Contains(channel1Id) && ids.Contains(channel2Id)),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexOnSuccess()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new ManageModel.InputModel
        {
            EventType = EventTypes.CheckCreated,
            SelectedChannelIds = []
        };

        _mockEventSubscriptionCommand.UpdateEventSubscriptionsAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UpdateEventSubscriptionsResult
            {
                Success = true,
                CreatedCount = 0,
                DeletedCount = 0
            }));

        var result = await _pageModel.OnPostAsync(workspaceId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageInTempData()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new ManageModel.InputModel
        {
            EventType = EventTypes.CheckUpdated,
            SelectedChannelIds = []
        };

        _mockEventSubscriptionCommand.UpdateEventSubscriptionsAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new UpdateEventSubscriptionsResult
            {
                Success = true,
                CreatedCount = 1,
                DeletedCount = 2
            }));

        await _pageModel.OnPostAsync(workspaceId);

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "1 added");
        StringAssert.Contains(message, "2 removed");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldLoadChannelsWhenModelStateIsInvalid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var expectedChannels = new List<EventSubscriptionChannelViewModel>
        {
            new() { Id = Guid.NewGuid(), Name = "Channel 1", ChannelType = "Email", Enabled = true, IsSubscribed = false }
        };
        _mockEventSubscriptionQuery.GetChannelsForEventTypeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedChannels));

        _pageModel.ModelState.AddModelError("Input.EventType", "Required");
        _pageModel.Input.EventType = EventTypes.CheckCreated;

        await _pageModel.OnPostAsync(workspaceId);

        Assert.HasCount(1, _pageModel.Channels);
        await _mockEventSubscriptionQuery.Received(1).GetChannelsForEventTypeAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallCommandServiceWhenModelStateIsInvalid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetChannelsForEventTypeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionChannelViewModel>()));

        _pageModel.ModelState.AddModelError("Input.EventType", "Required");
        _pageModel.Input.EventType = EventTypes.CheckCreated;

        await _pageModel.OnPostAsync(workspaceId);

        await _mockEventSubscriptionCommand.DidNotReceive().UpdateEventSubscriptionsAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
