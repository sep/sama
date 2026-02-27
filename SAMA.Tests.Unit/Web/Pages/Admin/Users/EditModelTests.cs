using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Pages.Admin.Users;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Admin.Users;

[TestClass]
public class EditModelTests
{
    private UserQueryService _mockUserQuery = null!;
    private UserCommandService _mockUserCommand = null!;
    private EditModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockUserQuery = Substitute.For<UserQueryService>(null!, null!);
        _mockUserCommand = Substitute.For<UserCommandService>(null!, null!, null!);

        _pageModel = new EditModel(_mockUserQuery, _mockUserCommand);
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
        _mockUserQuery.GetWorkspacesWithManualAssignmentStatusAsync(userId)
            .Returns(Task.FromResult(new List<WorkspaceAssignmentViewModel>()));

        var result = await _pageModel.OnGetAsync(userId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateInputFromUser()
    {
        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "edit@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = true,
            IsLockedOut = false
        };
        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserQuery.GetWorkspacesWithManualAssignmentStatusAsync(userId)
            .Returns(Task.FromResult(new List<WorkspaceAssignmentViewModel>()));

        await _pageModel.OnGetAsync(userId);

        Assert.AreEqual(userId, _pageModel.Input.Id);
        Assert.AreEqual("edit@example.com", _pageModel.Input.Email);
        Assert.IsTrue(_pageModel.Input.IsAdmin);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadWorkspaceAssignments()
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
        var workspaces = new List<WorkspaceAssignmentViewModel>
        {
            new() { WorkspaceId = Guid.NewGuid(), WorkspaceName = "Workspace 1", Role = AuthConstants.EditorRole, IsAssigned = true },
            new() { WorkspaceId = Guid.NewGuid(), WorkspaceName = "Workspace 2", Role = AuthConstants.ViewerRole, IsAssigned = false }
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserQuery.GetWorkspacesWithManualAssignmentStatusAsync(userId)
            .Returns(Task.FromResult(workspaces));

        await _pageModel.OnGetAsync(userId);

        Assert.HasCount(2, _pageModel.Input.WorkspaceAssignments);
        Assert.AreEqual("Workspace 1", _pageModel.Input.WorkspaceAssignments[0].WorkspaceName);
        Assert.IsTrue(_pageModel.Input.WorkspaceAssignments[0].IsAssigned);
        Assert.AreEqual(AuthConstants.EditorRole, _pageModel.Input.WorkspaceAssignments[0].Role);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetWorkspacesWithManualAssignmentStatusAsync()
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
        _mockUserQuery.GetWorkspacesWithManualAssignmentStatusAsync(userId)
            .Returns(Task.FromResult(new List<WorkspaceAssignmentViewModel>()));

        await _pageModel.OnGetAsync(userId);

        await _mockUserQuery.Received(1).GetWorkspacesWithManualAssignmentStatusAsync(userId);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenModelStateIsInvalid()
    {
        _mockUserQuery.GetWorkspacesWithManualAssignmentStatusAsync(Arg.Any<Guid?>())
            .Returns(Task.FromResult(new List<WorkspaceAssignmentViewModel>()));

        _pageModel.ModelState.AddModelError("Input.Email", "Email is required");
        _pageModel.Input = new EditModel.InputModel
        {
            Id = Guid.NewGuid(),
            Email = "",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenUserDoesNotExist()
    {
        var userId = Guid.NewGuid();
        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "test@example.com",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(null));

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallUpdateUserEmailAsyncWhenEmailChanged()
    {
        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "old@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserCommand.UpdateWorkspaceAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<List<WorkspaceAssignmentViewModel>>(), Arg.Any<string>())
            .Returns(Task.FromResult(UpdateWorkspaceAssignmentsResult.SuccessResult(0, 0, 0)));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "new@example.com",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        await _pageModel.OnPostAsync();

        await _mockUserCommand.Received(1).UpdateUserEmailAsync(userId, "new@example.com", Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallUpdateUserEmailAsyncWhenEmailUnchanged()
    {
        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "same@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserCommand.UpdateWorkspaceAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<List<WorkspaceAssignmentViewModel>>(), Arg.Any<string>())
            .Returns(Task.FromResult(UpdateWorkspaceAssignmentsResult.SuccessResult(0, 0, 0)));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "same@example.com",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        await _pageModel.OnPostAsync();

        await _mockUserCommand.DidNotReceive().UpdateUserEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallGrantAdminRoleAsyncWhenAdminStatusChangedToTrue()
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
        _mockUserCommand.UpdateWorkspaceAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<List<WorkspaceAssignmentViewModel>>(), Arg.Any<string>())
            .Returns(Task.FromResult(UpdateWorkspaceAssignmentsResult.SuccessResult(0, 0, 0)));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "test@example.com",
            IsAdmin = true,
            WorkspaceAssignments = []
        };

        await _pageModel.OnPostAsync();

        await _mockUserCommand.Received(1).GrantAdminRoleAsync(userId, Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallRevokeAdminRoleAsyncWhenAdminStatusChangedToFalse()
    {
        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "test@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = true,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserCommand.UpdateWorkspaceAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<List<WorkspaceAssignmentViewModel>>(), Arg.Any<string>())
            .Returns(Task.FromResult(UpdateWorkspaceAssignmentsResult.SuccessResult(0, 0, 0)));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "test@example.com",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        await _pageModel.OnPostAsync();

        await _mockUserCommand.Received(1).RevokeAdminRoleAsync(userId, Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotChangeRoleWhenAdminStatusUnchanged()
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
        _mockUserCommand.UpdateWorkspaceAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<List<WorkspaceAssignmentViewModel>>(), Arg.Any<string>())
            .Returns(Task.FromResult(UpdateWorkspaceAssignmentsResult.SuccessResult(0, 0, 0)));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "test@example.com",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        await _pageModel.OnPostAsync();

        await _mockUserCommand.DidNotReceive().GrantAdminRoleAsync(Arg.Any<Guid>(), Arg.Any<string>());
        await _mockUserCommand.DidNotReceive().RevokeAdminRoleAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallUpdateWorkspaceAssignmentsAsync()
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

        var workspaceAssignments = new List<WorkspaceAssignmentViewModel>
        {
            new() { WorkspaceId = Guid.NewGuid(), WorkspaceName = "Workspace 1", Role = AuthConstants.EditorRole, IsAssigned = true }
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserCommand.UpdateWorkspaceAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<List<WorkspaceAssignmentViewModel>>(), Arg.Any<string>())
            .Returns(Task.FromResult(UpdateWorkspaceAssignmentsResult.SuccessResult(1, 0, 0)));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "test@example.com",
            IsAdmin = false,
            WorkspaceAssignments = workspaceAssignments
        };

        await _pageModel.OnPostAsync();

        await _mockUserCommand.Received(1).UpdateWorkspaceAssignmentsAsync(
            userId,
            Arg.Is<List<WorkspaceAssignmentViewModel>>(list => list.Count == 1),
            Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToDetailsAfterSuccessfulUpdate()
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
        _mockUserCommand.UpdateWorkspaceAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<List<WorkspaceAssignmentViewModel>>(), Arg.Any<string>())
            .Returns(Task.FromResult(UpdateWorkspaceAssignmentsResult.SuccessResult(0, 0, 0)));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "test@example.com",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Details", redirect.PageName);
        Assert.AreEqual(userId, redirect.RouteValues?["id"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageInTempData()
    {
        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "updated@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserCommand.UpdateWorkspaceAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<List<WorkspaceAssignmentViewModel>>(), Arg.Any<string>())
            .Returns(Task.FromResult(UpdateWorkspaceAssignmentsResult.SuccessResult(0, 0, 0)));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "updated@example.com",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "updated@example.com");
        StringAssert.Contains(message, "updated successfully");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenEmailUpdateThrows()
    {
        var userId = Guid.NewGuid();
        var user = new UserViewModel
        {
            Id = userId,
            Email = "old@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
            IsAdmin = false,
            IsLockedOut = false
        };

        _mockUserQuery.GetUserByIdAsync(userId).Returns(Task.FromResult<UserViewModel?>(user));
        _mockUserQuery.GetWorkspacesWithManualAssignmentStatusAsync(Arg.Any<Guid?>())
            .Returns(Task.FromResult(new List<WorkspaceAssignmentViewModel>()));
        _mockUserCommand.When(x => x.UpdateUserEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(x => throw new InvalidOperationException("Email already exists"));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "new@example.com",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRepopulateWorkspaceNamesWhenReturningPage()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        _mockUserQuery.GetWorkspacesWithManualAssignmentStatusAsync(null)
            .Returns(Task.FromResult(new List<WorkspaceAssignmentViewModel>
            {
                new() { WorkspaceId = workspaceId, WorkspaceName = "Test Workspace", Role = AuthConstants.ViewerRole, IsAssigned = false }
            }));

        _pageModel.ModelState.AddModelError("Input.Email", "Invalid email");
        _pageModel.Input = new EditModel.InputModel
        {
            Id = userId,
            Email = "test@example.com",
            IsAdmin = false,
            WorkspaceAssignments =
            [
                new() { WorkspaceId = workspaceId, WorkspaceName = "", Role = AuthConstants.ViewerRole, IsAssigned = false }
            ]
        };

        await _pageModel.OnPostAsync();

        Assert.AreEqual("Test Workspace", _pageModel.Input.WorkspaceAssignments[0].WorkspaceName);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallCommandServicesWhenModelStateIsInvalid()
    {
        _mockUserQuery.GetWorkspacesWithManualAssignmentStatusAsync(Arg.Any<Guid?>())
            .Returns(Task.FromResult(new List<WorkspaceAssignmentViewModel>()));

        _pageModel.ModelState.AddModelError("Input.Email", "Invalid");
        _pageModel.Input = new EditModel.InputModel
        {
            Id = Guid.NewGuid(),
            Email = "",
            IsAdmin = false,
            WorkspaceAssignments = []
        };

        await _pageModel.OnPostAsync();

        await _mockUserCommand.DidNotReceive().UpdateUserEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>());
        await _mockUserCommand.DidNotReceive().GrantAdminRoleAsync(Arg.Any<Guid>(), Arg.Any<string>());
        await _mockUserCommand.DidNotReceive().RevokeAdminRoleAsync(Arg.Any<Guid>(), Arg.Any<string>());
        await _mockUserCommand.DidNotReceive().UpdateWorkspaceAssignmentsAsync(Arg.Any<Guid>(), Arg.Any<List<WorkspaceAssignmentViewModel>>(), Arg.Any<string>());
    }
}
