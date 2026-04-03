using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Pages.EventSubscriptions;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.EventSubscriptions;

[TestClass]
public class IndexModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private EventSubscriptionQueryService _mockEventSubscriptionQuery = null!;
    private IndexModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockEventSubscriptionQuery = Substitute.For<EventSubscriptionQueryService>((SamaDbContext)null!);

        _pageModel = new IndexModel(_mockWorkspaceQuery, _mockEventSubscriptionQuery);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
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
    public async Task OnGetAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenWorkspaceExists()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetEventSubscriptionGroupsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionGroupViewModel>()));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadEventSubscriptionGroupsFromQueryService()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var expectedGroups = new List<EventSubscriptionGroupViewModel>
        {
            new()
            {
                EventType = EventTypes.CheckCreated,
                SubscribedChannelCount = 2,
                TotalChannelCount = 3,
                SubscribedChannelNames = ["Channel 1", "Channel 2"]
            },
            new()
            {
                EventType = EventTypes.CheckUpdated,
                SubscribedChannelCount = 0,
                TotalChannelCount = 3,
                SubscribedChannelNames = []
            }
        };
        _mockEventSubscriptionQuery.GetEventSubscriptionGroupsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedGroups));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.HasCount(2, _pageModel.EventSubscriptionGroups);
        Assert.AreEqual(EventTypes.CheckCreated, _pageModel.EventSubscriptionGroups[0].EventType);
        Assert.AreEqual(2, _pageModel.EventSubscriptionGroups[0].SubscribedChannelCount);
        Assert.AreEqual(EventTypes.CheckUpdated, _pageModel.EventSubscriptionGroups[1].EventType);
        Assert.AreEqual(0, _pageModel.EventSubscriptionGroups[1].SubscribedChannelCount);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetWorkspaceIdProperty()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetEventSubscriptionGroupsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionGroupViewModel>()));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(workspaceId, _pageModel.WorkspaceId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetWorkspaceNameProperty()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "My Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetEventSubscriptionGroupsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionGroupViewModel>()));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual("My Workspace", _pageModel.WorkspaceName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldNotLoadGroupsWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        await _pageModel.OnGetAsync(workspaceId);

        await _mockEventSubscriptionQuery.DidNotReceive().GetEventSubscriptionGroupsForWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetEventSubscriptionGroupsForWorkspaceAsyncWithCorrectId()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetEventSubscriptionGroupsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionGroupViewModel>()));

        await _pageModel.OnGetAsync(workspaceId);

        await _mockEventSubscriptionQuery.Received(1).GetEventSubscriptionGroupsForWorkspaceAsync(
            workspaceId,
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateEmptyListWhenNoGroups()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockEventSubscriptionQuery.GetEventSubscriptionGroupsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<EventSubscriptionGroupViewModel>()));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.IsEmpty(_pageModel.EventSubscriptionGroups);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldHandleAllEventTypes()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var allEventTypes = new List<EventSubscriptionGroupViewModel>
        {
            new() { EventType = EventTypes.CheckCreated, SubscribedChannelCount = 1, TotalChannelCount = 2 },
            new() { EventType = EventTypes.CheckUpdated, SubscribedChannelCount = 2, TotalChannelCount = 2 },
            new() { EventType = EventTypes.CheckDeleted, SubscribedChannelCount = 0, TotalChannelCount = 2 },
            new() { EventType = EventTypes.CheckStatusChanged, SubscribedChannelCount = 1, TotalChannelCount = 2 }
        };
        _mockEventSubscriptionQuery.GetEventSubscriptionGroupsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(allEventTypes));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.HasCount(4, _pageModel.EventSubscriptionGroups);
        Assert.IsTrue(_pageModel.EventSubscriptionGroups.Any(g => g.EventType == EventTypes.CheckCreated));
        Assert.IsTrue(_pageModel.EventSubscriptionGroups.Any(g => g.EventType == EventTypes.CheckUpdated));
        Assert.IsTrue(_pageModel.EventSubscriptionGroups.Any(g => g.EventType == EventTypes.CheckDeleted));
        Assert.IsTrue(_pageModel.EventSubscriptionGroups.Any(g => g.EventType == EventTypes.CheckStatusChanged));
    }
}
