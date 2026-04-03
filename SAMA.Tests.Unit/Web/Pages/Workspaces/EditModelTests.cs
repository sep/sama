using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Pages.Workspaces;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Workspaces;

[TestClass]
public class EditModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private WorkspaceCommandService _mockWorkspaceCommand = null!;
    private MarkdownService _markdownService = null!;
    private EditModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockWorkspaceCommand = Substitute.For<WorkspaceCommandService>(null!, null!);
        _markdownService = new MarkdownService();

        _pageModel = new EditModel(_mockWorkspaceQuery, _mockWorkspaceCommand, _markdownService);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldRedirectWhenIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenWorkspaceExists()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = "Test Workspace",
            Description = "Test Description",
            IsPublic = true
        };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateInputFromWorkspace()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = "Edit Workspace",
            Description = "Edit Description",
            IsPublic = false
        };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(workspaceId, _pageModel.Input.Id);
        Assert.AreEqual("Edit Workspace", _pageModel.Input.Name);
        Assert.AreEqual("Edit Description", _pageModel.Input.Description);
        Assert.IsFalse(_pageModel.Input.IsPublic);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetViewDataForLayout()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = "Layout Workspace",
            Description = null,
            IsPublic = true
        };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(workspaceId.ToString("D"), _pageModel.ViewData["WorkspaceId"]);
        Assert.AreEqual("Layout Workspace", _pageModel.ViewData["WorkspaceName"]);
        Assert.AreEqual("Settings", _pageModel.ViewData["ActiveTab"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenModelStateIsInvalid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = "Test Workspace",
            IsPublic = false
        };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new EditModel.InputModel { Id = workspaceId };
        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallUpdateWorkspaceAsyncWithCorrectParameters()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.UpdateWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = workspaceId,
            Name = "Updated Workspace",
            Description = "Updated Description",
            IsPublic = true
        };

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.Received(1).UpdateWorkspaceAsync(
            workspaceId,
            "Updated Workspace",
            "Updated Description",
            Arg.Any<string?>(),
            true,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToDashboardAfterSuccessfulUpdate()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.UpdateWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = workspaceId,
            Name = "Test Workspace",
            IsPublic = false
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Dashboard/Index", redirect.PageName);
        Assert.AreEqual(workspaceId, redirect.RouteValues?["workspaceId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageInTempData()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.UpdateWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = workspaceId,
            Name = "Success Workspace",
            IsPublic = false
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        Assert.Contains("Success Workspace", message);
        Assert.Contains("updated successfully", message);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnBadRequestWhenUpdateFails()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.UpdateWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = workspaceId,
            Name = "Test Workspace",
            IsPublic = false
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<BadRequestResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallUpdateWorkspaceAsyncWhenModelStateIsInvalid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = "Test Workspace",
            IsPublic = false
        };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new EditModel.InputModel { Id = workspaceId };
        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.DidNotReceive().UpdateWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRepopulateViewDataWhenValidationFails()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = "Test Workspace",
            IsPublic = false
        };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new EditModel.InputModel { Id = workspaceId };
        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        await _pageModel.OnPostAsync();

        Assert.AreEqual(workspaceId.ToString("D"), _pageModel.ViewData["WorkspaceId"]);
        Assert.AreEqual("Test Workspace", _pageModel.ViewData["WorkspaceName"]);
        Assert.AreEqual("Settings", _pageModel.ViewData["ActiveTab"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldHandleNullDescription()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.UpdateWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = workspaceId,
            Name = "Workspace Without Description",
            Description = null,
            IsPublic = true
        };

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.Received(1).UpdateWorkspaceAsync(
            workspaceId,
            "Workspace Without Description",
            Arg.Is<string?>(d => d == null),
            Arg.Any<string?>(),
            true,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldToggleIsPublicFlag()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.UpdateWorkspaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = workspaceId,
            Name = "Test",
            IsPublic = false
        };

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.Received(1).UpdateWorkspaceAsync(
            workspaceId,
            "Test",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            false,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnGetAsyncShouldHandleWorkspaceWithoutDescription()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace
        {
            Id = workspaceId,
            Name = "No Description",
            Description = null,
            IsPublic = true
        };

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.IsNull(_pageModel.Input.Description);
    }
}
