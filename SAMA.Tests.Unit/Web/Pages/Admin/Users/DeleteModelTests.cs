using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
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
public class DeleteModelTests
{
    private UserQueryService _mockUserQuery = null!;
    private UserCommandService _mockUserCommand = null!;
    private DeleteModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockUserQuery = Substitute.For<UserQueryService>(null!, null!);
        _mockUserCommand = Substitute.For<UserCommandService>(null!, null!, null!);

        _pageModel = new DeleteModel(_mockUserQuery, _mockUserCommand);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnRedirectWhenDeletingSelf()
    {
        var currentUserId = Guid.NewGuid();
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, currentUserId, "current@example.com");

        var result = await _pageModel.OnGetAsync(currentUserId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.IsTrue(_pageModel.TempData.ContainsKey("ErrorMessage"));
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenUserDoesNotExist()
    {
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, Guid.NewGuid(), "current@example.com");

        var currentUserId = Guid.NewGuid();
        _mockUserQuery.GetUserByIdAsync(currentUserId).Returns(Task.FromResult<UserViewModel?>(null));

        var result = await _pageModel.OnGetAsync(currentUserId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenUserExists()
    {
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, Guid.NewGuid(), "current@example.com");

        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "delete@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));

        var result = await _pageModel.OnGetAsync(userId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateUserToDeleteProperty()
    {
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, Guid.NewGuid(), "current@example.com");

        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "delete@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = true,
            IsLockedOut = false,
            Workspaces = [new(), new(), new(), new(), new()]
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));

        await _pageModel.OnGetAsync(userId);

        Assert.AreEqual(userId, _pageModel.UserToDelete.Id);
        Assert.AreEqual("delete@example.com", _pageModel.UserToDelete.Email);
        Assert.IsTrue(_pageModel.UserToDelete.IsAdmin);
        Assert.AreEqual(5, _pageModel.UserToDelete.Workspaces.Count);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnPostAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnRedirectWhenDeletingSelf()
    {
        var userId = Guid.NewGuid();
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, userId, "current@example.com");

        var result = await _pageModel.OnPostAsync(userId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.IsTrue(_pageModel.TempData.ContainsKey("ErrorMessage"));
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenUserDoesNotExist()
    {
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, Guid.NewGuid(), "current@example.com");

        var userId = Guid.NewGuid();
        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(null));

        var result = await _pageModel.OnPostAsync(userId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallDeleteUserAsyncWithCorrectId()
    {
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, Guid.NewGuid(), "current@example.com");

        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "delete@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));

        await _pageModel.OnPostAsync(userId);

        await _mockUserCommand.Received(1).DeleteUserAsync(userId, Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulDeletion()
    {
        var userId = Guid.NewGuid();
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, userId, "current@example.com");
        var user = new UserViewModel
        {
            Id = userId,
            Email = "delete@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));

        var result = await _pageModel.OnPostAsync(userId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageInTempData()
    {
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, Guid.NewGuid(), "current@example.com");

        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "deleted@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));

        await _pageModel.OnPostAsync(userId);

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        Assert.Contains("deleted@example.com", message);
        Assert.Contains("deleted successfully", message);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenDeleteUserAsyncThrows()
    {
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, Guid.NewGuid(), "current@example.com");

        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "delete@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserCommand.When(x => x.DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<string>()))
            .Do(x => throw new InvalidOperationException("Cannot delete user"));

        var result = await _pageModel.OnPostAsync(userId);

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRepopulateUserToDeleteWhenExceptionOccurs()
    {
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, Guid.NewGuid(), "current@example.com");

        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "delete@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false,
            Workspaces = [new(), new(), new()]
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserCommand.When(x => x.DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<string>()))
            .Do(x => throw new InvalidOperationException("Cannot delete user"));

        await _pageModel.OnPostAsync(userId);

        Assert.AreEqual(userId, _pageModel.UserToDelete.Id);
        Assert.AreEqual("delete@example.com", _pageModel.UserToDelete.Email);
        Assert.AreEqual(3, _pageModel.UserToDelete.Workspaces.Count);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallDeleteUserAsyncWhenDeletingSelf()
    {
        var userId = Guid.NewGuid();
        PageModelTestHelpers.ConfigureAuthenticatedUser(_pageModel, userId, "current@example.com");

        await _pageModel.OnPostAsync(userId);

        await _mockUserCommand.DidNotReceive().DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }
}
