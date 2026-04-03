using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.NotificationChannels;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.NotificationChannels;

[TestClass]
public class CreateModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private NotificationChannelConfigurationService _mockConfigService = null!;
    private ChannelCommandService _mockChannelCommand = null!;
    private CreateModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockConfigService = Substitute.For<NotificationChannelConfigurationService>();
        _mockChannelCommand = Substitute.For<ChannelCommandService>(null!, null!);

        _pageModel = new CreateModel(_mockWorkspaceQuery, _mockConfigService, _mockChannelCommand);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenWorkspaceExists()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetWorkspaceIdInInput()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(workspaceId, _pageModel.Input.WorkspaceId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateChannelTypes()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.IsNotEmpty(_pageModel.ChannelTypes);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallCreateChannelAsyncWithCorrectParameters()
    {
        var workspaceId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        _mockConfigService.BuildConfiguration(Arg.Any<NotificationChannelInputBase>())
            .Returns([]);
        _mockChannelCommand.CreateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channelId));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Test Channel",
            ChannelType = "Email",
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        await _mockChannelCommand.Received(1).CreateChannelAsync(
            workspaceId,
            "Test Channel",
            "Email",
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            true,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulCreation()
    {
        var workspaceId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        _mockConfigService.BuildConfiguration(Arg.Any<NotificationChannelInputBase>())
            .Returns([]);
        _mockChannelCommand.CreateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channelId));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "New Channel",
            ChannelType = "Slack",
            Enabled = false
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.AreEqual(workspaceId, redirect.RouteValues?["workspaceId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenModelStateIsInvalid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input.WorkspaceId = workspaceId;
        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessage()
    {
        var workspaceId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        _mockConfigService.BuildConfiguration(Arg.Any<NotificationChannelInputBase>())
            .Returns([]);
        _mockChannelCommand.CreateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channelId));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Success Channel",
            ChannelType = "Email",
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.Contains("Success Channel", message!);
        Assert.Contains("created successfully", message!);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallValidateConfiguration()
    {
        var workspaceId = Guid.NewGuid();
        var channelId = Guid.NewGuid();

        _mockConfigService.BuildConfiguration(Arg.Any<CreateModel.InputModel>())
            .Returns([]);
        _mockChannelCommand.CreateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channelId));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Test",
            ChannelType = "Email",
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        _mockConfigService.Received(1).ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Is<CreateModel.InputModel>(i => i.Name == "Test"));
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCreateWhenValidationFails()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _mockConfigService
            .When(x => x.ValidateConfiguration(Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(), Arg.Any<CreateModel.InputModel>()))
            .Do(x => x.Arg<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>().AddModelError("Email", "Invalid"));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Test",
            ChannelType = "Email"
        };

        await _pageModel.OnPostAsync();

        await _mockChannelCommand.DidNotReceive().CreateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
