using NSubstitute;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Web.Pages.Workspaces;

[TestClass]
public class IndexModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private WorkspaceAuthorizationService _mockAuthService = null!;
    private IndexModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockAuthService = Substitute.For<WorkspaceAuthorizationService>(null!, null!);

        _pageModel = new IndexModel(_mockWorkspaceQuery, _mockAuthService);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadAccessibleWorkspacesForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, userId, "test@example.com");

        var workspaceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var workspaces = new List<WorkspaceDetailsViewModel>
        {
            new()
            {
                Id = workspaceIds[0],
                Name = "Workspace 1",
                Description = "Description 1",
                IsPublic = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CheckCount = 5,
                NotificationChannelCount = 3,
                UserCount = 2
            },
            new()
            {
                Id = workspaceIds[1],
                Name = "Workspace 2",
                Description = null,
                IsPublic = true,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                CheckCount = 0,
                NotificationChannelCount = 0,
                UserCount = 0
            }
        };

        _mockAuthService.IsGlobalAdmin(userId).Returns(Task.FromResult(false));
        _mockAuthService.GetAccessibleWorkspaceIds(userId).Returns(Task.FromResult(workspaceIds));
        _mockWorkspaceQuery.GetWorkspacesAsync(workspaceIds, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(workspaces));

        await _pageModel.OnGetAsync();

        Assert.HasCount(2, _pageModel.Workspaces);
        Assert.AreEqual("Workspace 1", _pageModel.Workspaces[0].Name);
        Assert.AreEqual("Workspace 2", _pageModel.Workspaces[1].Name);
        Assert.IsFalse(_pageModel.IsAdmin);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadPublicWorkspacesForGuests()
    {
        var workspaceIds = new List<Guid> { Guid.NewGuid() };
        var workspaces = new List<WorkspaceDetailsViewModel>
        {
            new()
            {
                Id = workspaceIds[0],
                Name = "Public Workspace",
                Description = "Public Description",
                IsPublic = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CheckCount = 3,
                NotificationChannelCount = 1,
                UserCount = 0
            }
        };

        _mockAuthService.GetAccessibleWorkspaceIds(null).Returns(Task.FromResult(workspaceIds));
        _mockWorkspaceQuery.GetWorkspacesAsync(workspaceIds, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(workspaces));

        await _pageModel.OnGetAsync();

        Assert.HasCount(1, _pageModel.Workspaces);
        Assert.AreEqual("Public Workspace", _pageModel.Workspaces[0].Name);
        Assert.IsFalse(_pageModel.IsAdmin);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetIsAdminTrueForGlobalAdmin()
    {
        var userId = Guid.NewGuid();
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, userId, "admin@example.com");

        _mockAuthService.IsGlobalAdmin(userId).Returns(Task.FromResult(true));
        _mockAuthService.GetAccessibleWorkspaceIds(userId).Returns(Task.FromResult(new List<Guid>()));
        _mockWorkspaceQuery.GetWorkspacesAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<WorkspaceDetailsViewModel>()));

        await _pageModel.OnGetAsync();

        Assert.IsTrue(_pageModel.IsAdmin);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetIsAdminFalseForNonAdmin()
    {
        var userId = Guid.NewGuid();
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, userId, "user@example.com");

        _mockAuthService.IsGlobalAdmin(userId).Returns(Task.FromResult(false));
        _mockAuthService.GetAccessibleWorkspaceIds(userId).Returns(Task.FromResult(new List<Guid>()));
        _mockWorkspaceQuery.GetWorkspacesAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<WorkspaceDetailsViewModel>()));

        await _pageModel.OnGetAsync();

        Assert.IsFalse(_pageModel.IsAdmin);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateEmptyListWhenNoWorkspaces()
    {
        _mockAuthService.GetAccessibleWorkspaceIds(null).Returns(Task.FromResult(new List<Guid>()));
        _mockWorkspaceQuery.GetWorkspacesAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<WorkspaceDetailsViewModel>()));

        await _pageModel.OnGetAsync();

        Assert.IsEmpty(_pageModel.Workspaces);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetAccessibleWorkspaceIdsWithUserId()
    {
        var userId = Guid.NewGuid();
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, userId, "user@example.com");

        _mockAuthService.IsGlobalAdmin(userId).Returns(Task.FromResult(false));
        _mockAuthService.GetAccessibleWorkspaceIds(userId).Returns(Task.FromResult(new List<Guid>()));
        _mockWorkspaceQuery.GetWorkspacesAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<WorkspaceDetailsViewModel>()));

        await _pageModel.OnGetAsync();

        await _mockAuthService.Received(1).GetAccessibleWorkspaceIds(userId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetAccessibleWorkspaceIdsWithNullForGuests()
    {
        _mockAuthService.GetAccessibleWorkspaceIds(null).Returns(Task.FromResult(new List<Guid>()));
        _mockWorkspaceQuery.GetWorkspacesAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<WorkspaceDetailsViewModel>()));

        await _pageModel.OnGetAsync();

        await _mockAuthService.Received(1).GetAccessibleWorkspaceIds(null);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetWorkspacesAsyncWithCorrectIds()
    {
        var workspaceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _mockAuthService.GetAccessibleWorkspaceIds(null).Returns(Task.FromResult(workspaceIds));
        _mockWorkspaceQuery.GetWorkspacesAsync(workspaceIds, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<WorkspaceDetailsViewModel>()));

        await _pageModel.OnGetAsync();

        await _mockWorkspaceQuery.Received(1).GetWorkspacesAsync(
            Arg.Is<List<Guid>>(ids => ids.SequenceEqual(workspaceIds)),
            Arg.Any<CancellationToken>());
    }
}
