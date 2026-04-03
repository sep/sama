using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Workspaces;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Workspaces;

[TestClass]
public class DeleteModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private WorkspaceCommandService _mockWorkspaceCommand = null!;
    private DeleteModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockWorkspaceCommand = Substitute.For<WorkspaceCommandService>(null!, null!);

        _pageModel = new DeleteModel(_mockWorkspaceQuery, _mockWorkspaceCommand);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceDetailsAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkspaceDetailsViewModel?>(null));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenWorkspaceExists()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new WorkspaceDetailsViewModel
        {
            Id = workspaceId,
            Name = "Test Workspace",
            Description = "Test Description",
            IsPublic = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CheckCount = 5,
            NotificationChannelCount = 3,
            UserCount = 2
        };

        _mockWorkspaceQuery.GetWorkspaceDetailsAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkspaceDetailsViewModel?>(workspace));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateWorkspaceToDelete()
    {
        var workspaceId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var updatedAt = new DateTimeOffset(2024, 1, 20, 14, 45, 0, TimeSpan.Zero);
        var workspace = new WorkspaceDetailsViewModel
        {
            Id = workspaceId,
            Name = "Delete Workspace",
            Description = "To be deleted",
            IsPublic = false,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            CheckCount = 10,
            NotificationChannelCount = 5,
            UserCount = 3
        };

        _mockWorkspaceQuery.GetWorkspaceDetailsAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkspaceDetailsViewModel?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(workspaceId, _pageModel.WorkspaceToDelete.Id);
        Assert.AreEqual("Delete Workspace", _pageModel.WorkspaceToDelete.Name);
        Assert.AreEqual("To be deleted", _pageModel.WorkspaceToDelete.Description);
        Assert.IsFalse(_pageModel.WorkspaceToDelete.IsPublic);
        Assert.AreEqual(createdAt, _pageModel.WorkspaceToDelete.CreatedAt);
        Assert.AreEqual(updatedAt, _pageModel.WorkspaceToDelete.UpdatedAt);
        Assert.AreEqual(10, _pageModel.WorkspaceToDelete.CheckCount);
        Assert.AreEqual(5, _pageModel.WorkspaceToDelete.NotificationChannelCount);
        Assert.AreEqual(3, _pageModel.WorkspaceToDelete.UserCount);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetWorkspaceDetailsAsync()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new WorkspaceDetailsViewModel
        {
            Id = workspaceId,
            Name = "Test Workspace",
            IsPublic = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CheckCount = 0,
            NotificationChannelCount = 0,
            UserCount = 0
        };

        _mockWorkspaceQuery.GetWorkspaceDetailsAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkspaceDetailsViewModel?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        await _mockWorkspaceQuery.Received(1).GetWorkspaceDetailsAsync(workspaceId, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnPostAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnPostAsync(workspaceId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallDeleteWorkspaceAsync()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Workspace to Delete" };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));
        _mockWorkspaceCommand.DeleteWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(workspaceId);

        await _mockWorkspaceCommand.Received(1).DeleteWorkspaceAsync(
            workspaceId,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulDeletion()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));
        _mockWorkspaceCommand.DeleteWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var result = await _pageModel.OnPostAsync(workspaceId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageInTempData()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Deleted Workspace" };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));
        _mockWorkspaceCommand.DeleteWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(workspaceId);

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        Assert.Contains("Deleted Workspace", message);
        Assert.Contains("deleted successfully", message);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnBadRequestWhenDeleteWorkspaceAsyncReturnsFalse()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));
        _mockWorkspaceCommand.DeleteWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        var result = await _pageModel.OnPostAsync(workspaceId);

        Assert.IsInstanceOfType<BadRequestResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallDeleteWorkspaceAsyncWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(null));

        await _pageModel.OnPostAsync(workspaceId);

        await _mockWorkspaceCommand.DidNotReceive().DeleteWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldPreserveWorkspaceNameForSuccessMessage()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Important Workspace" };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));
        _mockWorkspaceCommand.DeleteWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(workspaceId);

        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.Contains("Important Workspace", message!);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotSetSuccessMessageWhenDeletionFails()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));
        _mockWorkspaceCommand.DeleteWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        await _pageModel.OnPostAsync(workspaceId);

        Assert.IsFalse(_pageModel.TempData.ContainsKey("SuccessMessage"));
    }

    [TestMethod]
    public async Task OnGetAsyncShouldHandleWorkspaceWithoutDescription()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new WorkspaceDetailsViewModel
        {
            Id = workspaceId,
            Name = "No Description",
            Description = null,
            IsPublic = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            CheckCount = 0,
            NotificationChannelCount = 0,
            UserCount = 0
        };

        _mockWorkspaceQuery.GetWorkspaceDetailsAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkspaceDetailsViewModel?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.IsNull(_pageModel.WorkspaceToDelete.Description);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallGetWorkspaceByIdAsyncBeforeDeletion()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));
        _mockWorkspaceCommand.DeleteWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(workspaceId);

        await _mockWorkspaceQuery.Received(1).GetWorkspaceByIdAsync(workspaceId);
        await _mockWorkspaceCommand.Received(1).DeleteWorkspaceAsync(
            workspaceId,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
