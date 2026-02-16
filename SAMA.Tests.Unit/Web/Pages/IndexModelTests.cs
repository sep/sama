using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Pages;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Pages;

[TestClass]
public class IndexModelTests
{
    private WorkspaceAuthorizationService _mockAuthService = null!;
    private UserPreferencesService _mockUserPreferencesService = null!;
    private GlobalSettingsService _mockGlobalSettingsService = null!;
    private UserManager<ApplicationUser> _mockUserManager = null!;
    private IndexModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockAuthService = Substitute.For<WorkspaceAuthorizationService>(null!, null!);
        _mockUserManager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        _mockUserPreferencesService = Substitute.For<UserPreferencesService>(_mockUserManager);
        _mockGlobalSettingsService = Substitute.For<GlobalSettingsService>(null!, null!, null!, null!);

        _pageModel = new IndexModel(_mockAuthService, _mockUserPreferencesService, _mockGlobalSettingsService, _mockUserManager);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldRedirectToDashboardWhenSingleWorkspaceAccessible()
    {
        var workspaceId = Guid.NewGuid();
        _mockAuthService.GetAccessibleWorkspaceIds(Arg.Any<Guid?>()).Returns(Task.FromResult(new List<Guid> { workspaceId }));

        var result = await _pageModel.OnGetAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Dashboard/Index", redirect.PageName);
        Assert.IsNotNull(redirect.RouteValues);
        Assert.AreEqual(workspaceId, redirect.RouteValues["workspaceId"]);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldRedirectToWorkspacesIndexWhenMultipleWorkspacesAccessible()
    {
        var workspaceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _mockAuthService.GetAccessibleWorkspaceIds(Arg.Any<Guid?>()).Returns(Task.FromResult(workspaceIds));

        var result = await _pageModel.OnGetAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Workspaces/Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldShowPageWhenNoWorkspacesAccessible()
    {
        _mockAuthService.GetAccessibleWorkspaceIds(Arg.Any<Guid?>()).Returns(Task.FromResult(new List<Guid>()));

        var result = await _pageModel.OnGetAsync();

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetIsAuthenticatedTrueForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, userId, "user@example.com");
        _mockAuthService.GetAccessibleWorkspaceIds(userId).Returns(Task.FromResult(new List<Guid>()));

        await _pageModel.OnGetAsync();

        Assert.IsTrue(_pageModel.IsAuthenticated);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetIsAuthenticatedFalseForGuest()
    {
        _mockAuthService.GetAccessibleWorkspaceIds(null).Returns(Task.FromResult(new List<Guid>()));

        await _pageModel.OnGetAsync();

        Assert.IsFalse(_pageModel.IsAuthenticated);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldRedirectToAnonymousDefaultWorkspaceWhenConfigured()
    {
        var workspaceId = Guid.NewGuid();
        _mockAuthService.GetAccessibleWorkspaceIds(null).Returns(Task.FromResult(new List<Guid> { workspaceId }));
        _mockGlobalSettingsService.AnonymousDefaultWorkspaceId.Returns(workspaceId);

        var result = await _pageModel.OnGetAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Dashboard/Index", redirect.PageName);
        Assert.IsNotNull(redirect.RouteValues);
        Assert.AreEqual(workspaceId, redirect.RouteValues["workspaceId"]);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldNotRedirectToAnonymousDefaultWorkspaceWhenNotAccessible()
    {
        var defaultWorkspaceId = Guid.NewGuid();
        var accessibleWorkspaceId = Guid.NewGuid();
        _mockAuthService.GetAccessibleWorkspaceIds(null).Returns(Task.FromResult(new List<Guid> { accessibleWorkspaceId }));
        _mockGlobalSettingsService.AnonymousDefaultWorkspaceId.Returns(defaultWorkspaceId);

        var result = await _pageModel.OnGetAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Dashboard/Index", redirect.PageName);
        Assert.IsNotNull(redirect.RouteValues);
        Assert.AreEqual(accessibleWorkspaceId, redirect.RouteValues["workspaceId"]);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldShowWorkspaceListWhenNoAnonymousDefaultConfigured()
    {
        var workspaceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        _mockAuthService.GetAccessibleWorkspaceIds(null).Returns(Task.FromResult(workspaceIds));
        _mockGlobalSettingsService.AnonymousDefaultWorkspaceId.Returns((Guid?)null);

        var result = await _pageModel.OnGetAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Workspaces/Index", redirect.PageName);
    }
}
