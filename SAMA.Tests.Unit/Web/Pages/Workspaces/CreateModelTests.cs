using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Pages.Workspaces;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;

namespace SAMA.Tests.Unit.Web.Pages.Workspaces;

[TestClass]
public class CreateModelTests
{
    private WorkspaceCommandService _mockWorkspaceCommand = null!;
    private MarkdownService _mockMarkdownService = null!;
    private CreateModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceCommand = Substitute.For<WorkspaceCommandService>(null!, null!, null!);
        _mockMarkdownService = Substitute.For<MarkdownService>();

        _pageModel = new CreateModel(_mockWorkspaceCommand, _mockMarkdownService);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallCreateWorkspaceAsyncWithCorrectParameters()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.CreateWorkspaceAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(workspaceId));

        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "Test Workspace",
            Description = "Test Description",
            DashboardMessage = "Test Message",
            IsPublic = true
        };

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.Received(1).CreateWorkspaceAsync(
            "Test Workspace",
            "Test Description",
            "Test Message",
            true,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulCreation()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.CreateWorkspaceAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(workspaceId));

        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "New Workspace",
            IsPublic = false
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageInTempData()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.CreateWorkspaceAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(workspaceId));

        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "Success Workspace",
            IsPublic = false
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        Assert.Contains("Success Workspace", message);
        Assert.Contains("created successfully", message);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenModelStateIsInvalid()
    {
        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "",
            IsPublic = false
        };
        _pageModel.ModelState.AddModelError("Input.Name", "Workspace name is required");

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallCreateWorkspaceAsyncWhenModelStateIsInvalid()
    {
        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "",
            IsPublic = false
        };
        _pageModel.ModelState.AddModelError("Input.Name", "Workspace name is required");

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.DidNotReceive().CreateWorkspaceAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldHandleNullDescription()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.CreateWorkspaceAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(workspaceId));

        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "Workspace Without Description",
            Description = null,
            IsPublic = true
        };

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.Received(1).CreateWorkspaceAsync(
            "Workspace Without Description",
            Arg.Is<string?>(d => d == null),
            Arg.Any<string?>(),
            true,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCreatePublicWorkspace()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.CreateWorkspaceAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(workspaceId));

        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "Public Workspace",
            IsPublic = true
        };

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.Received(1).CreateWorkspaceAsync(
            "Public Workspace",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            true,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCreatePrivateWorkspace()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.CreateWorkspaceAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(workspaceId));

        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "Private Workspace",
            IsPublic = false
        };

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.Received(1).CreateWorkspaceAsync(
            "Private Workspace",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            false,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldPreserveWorkspaceNameForSuccessMessage()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.CreateWorkspaceAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(workspaceId));

        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "Important Workspace",
            IsPublic = false
        };

        await _pageModel.OnPostAsync();

        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.Contains("Important Workspace", message!);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCreateWorkspaceWithDescription()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceCommand.CreateWorkspaceAsync(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns(Task.FromResult(workspaceId));

        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "Described Workspace",
            Description = "A detailed description",
            IsPublic = true
        };

        await _pageModel.OnPostAsync();

        await _mockWorkspaceCommand.Received(1).CreateWorkspaceAsync(
            "Described Workspace",
            "A detailed description",
            Arg.Any<string?>(),
            true,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public void OnPostPreviewMarkdownShouldReturnRenderedHtml()
    {
        _mockMarkdownService.RenderToHtml("**bold**").Returns("<p><strong>bold</strong></p>");

        var result = _pageModel.OnPostPreviewMarkdown("**bold**");

        var contentResult = result as ContentResult;
        Assert.IsNotNull(contentResult);
        Assert.AreEqual("<p><strong>bold</strong></p>", contentResult.Content);
        Assert.AreEqual("text/html", contentResult.ContentType);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRenderPreviewWhenModelStateIsInvalid()
    {
        _mockMarkdownService.RenderToHtml("**Test**").Returns("<p><strong>Test</strong></p>");

        _pageModel.Input = new CreateModel.InputModel
        {
            Name = "",
            DashboardMessage = "**Test**"
        };
        _pageModel.ModelState.AddModelError("Input.Name", "Workspace name is required");

        await _pageModel.OnPostAsync();

        _mockMarkdownService.Received(1).RenderToHtml("**Test**");
        Assert.AreEqual("<p><strong>Test</strong></p>", _pageModel.DashboardMessagePreview);
    }
}
