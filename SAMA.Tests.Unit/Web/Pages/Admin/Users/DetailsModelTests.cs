using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Admin.Users;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Admin.Users;

[TestClass]
public class DetailsModelTests
{
    private UserQueryService _mockUserQuery = null!;
    private UserCommandService _mockUserCommand = null!;
    private DetailsModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockUserQuery = Substitute.For<UserQueryService>(null!, null!);
        _mockUserCommand = Substitute.For<UserCommandService>(null!, null!, null!);

        _pageModel = new DetailsModel(_mockUserQuery, _mockUserCommand);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(null));

        var result = await _pageModel.OnGetAsync(userId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenUserExists()
    {
        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "test@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };
        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));

        var result = await _pageModel.OnGetAsync(userId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateUserDetailsProperty()
    {
        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "user@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = true,
            IsLockedOut = false,
            Workspaces = [new(), new(), new()]
        };
        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));

        await _pageModel.OnGetAsync(userId);

        Assert.AreEqual(userId, _pageModel.UserDetails.Id);
        Assert.AreEqual("user@example.com", _pageModel.UserDetails.Email);
        Assert.IsTrue(_pageModel.UserDetails.IsAdmin);
        Assert.IsFalse(_pageModel.UserDetails.IsLockedOut);
        Assert.AreEqual(3, _pageModel.UserDetails.Workspaces.Count);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetUserByIdAsyncWithCorrectId()
    {
        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "test@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };
        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));

        await _pageModel.OnGetAsync(userId);

        await _mockUserQuery.Received(1).GetUserByIdAsync(userId);
    }

    [TestMethod]
    public async Task OnPostUnlockAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnPostUnlockAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostUnlockAsyncShouldCallUnlockUserAsyncWithCorrectId()
    {
        var userId = Guid.NewGuid();
        _mockUserCommand.UnlockUserAsync(userId, Arg.Any<string>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostUnlockAsync(userId);

        await _mockUserCommand.Received(1).UnlockUserAsync(userId, Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostUnlockAsyncShouldSetSuccessMessageWhenSuccessful()
    {
        var userId = Guid.NewGuid();
        _mockUserCommand.UnlockUserAsync(userId, Arg.Any<string>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostUnlockAsync(userId);

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "unlocked");
    }

    [TestMethod]
    public async Task OnPostUnlockAsyncShouldSetErrorMessageWhenFailed()
    {
        var userId = Guid.NewGuid();
        _mockUserCommand.UnlockUserAsync(userId, Arg.Any<string>()).Returns(Task.FromResult(false));

        await _pageModel.OnPostUnlockAsync(userId);

        Assert.IsTrue(_pageModel.TempData.ContainsKey("ErrorMessage"));
        var message = _pageModel.TempData["ErrorMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "Failed");
    }

    [TestMethod]
    public async Task OnPostUnlockAsyncShouldRedirectToDetailsPage()
    {
        var userId = Guid.NewGuid();
        _mockUserCommand.UnlockUserAsync(userId, Arg.Any<string>()).Returns(Task.FromResult(true));

        var result = await _pageModel.OnPostUnlockAsync(userId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual(userId, redirect.RouteValues?["id"]);
    }
}
