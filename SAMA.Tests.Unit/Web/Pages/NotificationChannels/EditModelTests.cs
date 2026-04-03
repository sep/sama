using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.NotificationChannels;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.NotificationChannels;

[TestClass]
public class EditModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private ChannelQueryService _mockChannelQuery = null!;
    private NotificationChannelConfigurationService _mockConfigService = null!;
    private ChannelCommandService _mockChannelCommand = null!;
    private EditModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockChannelQuery = Substitute.For<ChannelQueryService>(null!, null!);
        _mockConfigService = Substitute.For<NotificationChannelConfigurationService>();
        _mockChannelCommand = Substitute.For<ChannelCommandService>(null!, null!);

        _pageModel = new EditModel(_mockWorkspaceQuery, _mockChannelQuery, _mockConfigService, _mockChannelCommand);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenChannelDoesNotExist()
    {
        var channelId = Guid.NewGuid();
        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(null));

        var result = await _pageModel.OnGetAsync(channelId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenChannelExists()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(channelId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateInputFromChannel()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId, "Email Channel", "Email");
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(channelId);

        Assert.AreEqual(channelId, _pageModel.Input.Id);
        Assert.AreEqual(workspaceId, _pageModel.Input.WorkspaceId);
        Assert.AreEqual("Email Channel", _pageModel.Input.Name);
        Assert.AreEqual("Email", _pageModel.Input.ChannelType);
        Assert.IsTrue(_pageModel.Input.Enabled);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallPopulateFromConfiguration()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId)
            .Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(channelId);

        _mockConfigService.Received(1).PopulateFromConfiguration(
            Arg.Is<EditModel.InputModel>(i => i.Id == channelId),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallUpdateChannelAsyncWithCorrectParameters()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>())
            .Returns([]);
        _mockChannelCommand.UpdateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = channelId,
            WorkspaceId = workspaceId,
            Name = "Updated Channel",
            ChannelType = "Slack",
            Enabled = false
        };

        await _pageModel.OnPostAsync();

        await _mockChannelCommand.Received(1).UpdateChannelAsync(
            channelId,
            "Updated Channel",
            "Slack",
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            false,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenChannelDoesNotExist()
    {
        var channelId = Guid.NewGuid();

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(null));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = channelId,
            Name = "Test",
            ChannelType = "Email"
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulUpdate()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>())
            .Returns([]);
        _mockChannelCommand.UpdateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = channelId,
            WorkspaceId = workspaceId,
            Name = "Updated",
            ChannelType = "Email",
            Enabled = true
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.AreEqual(workspaceId, redirect.RouteValues?["workspaceId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessage()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>())
            .Returns([]);
        _mockChannelCommand.UpdateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = channelId,
            WorkspaceId = workspaceId,
            Name = "Success Channel",
            ChannelType = "Email",
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        StringAssert.Contains(message!, "Success Channel");
        StringAssert.Contains(message!, "updated successfully");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenModelStateIsInvalid()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = channelId,
            WorkspaceId = workspaceId,
            ChannelType = "Email"
        };
        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallValidateConfiguration()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channel = CreateTestChannel(channelId, workspaceId);

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>())
            .Returns([]);
        _mockChannelCommand.UpdateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = channelId,
            WorkspaceId = workspaceId,
            Name = "Test",
            ChannelType = "Email",
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        _mockConfigService.Received(1).ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Is<EditModel.InputModel>(i => i.Name == "Test"));
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotUpdateWhenValidationFails()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _mockConfigService
            .When(x => x.ValidateConfiguration(Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(), Arg.Any<EditModel.InputModel>()))
            .Do(x => x.Arg<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>().AddModelError("Email", "Invalid"));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = "Test",
            ChannelType = "Email"
        };

        await _pageModel.OnPostAsync();

        await _mockChannelCommand.DidNotReceive().UpdateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRestoreExistingEventGridAccessKeyWhenFieldIsEmpty()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var existingKey = "existing-access-key-12345";
        var channel = CreateTestEventGridChannel(channelId, workspaceId, existingKey);

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>())
            .Returns([]);
        _mockChannelCommand.UpdateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = channelId,
            WorkspaceId = workspaceId,
            Name = "EventGrid Channel",
            ChannelType = SAMA.Web.Constants.ChannelTypes.EventGrid,
            EventGridTopicEndpoint = "https://test.eventgrid.azure.net/api/events",
            EventGridAccessKey = "",
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        Assert.AreEqual(existingKey, _pageModel.Input.EventGridAccessKey);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldUseNewEventGridAccessKeyWhenProvided()
    {
        var channelId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var existingKey = "existing-access-key-12345";
        var newKey = "new-access-key-67890";
        var channel = CreateTestEventGridChannel(channelId, workspaceId, existingKey);

        _mockChannelQuery.GetChannelDetailsAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ChannelDetailsViewModel?>(channel));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>())
            .Returns([]);
        _mockChannelCommand.UpdateChannelAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = channelId,
            WorkspaceId = workspaceId,
            Name = "EventGrid Channel",
            ChannelType = SAMA.Web.Constants.ChannelTypes.EventGrid,
            EventGridTopicEndpoint = "https://test.eventgrid.azure.net/api/events",
            EventGridAccessKey = newKey,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        Assert.AreEqual(newKey, _pageModel.Input.EventGridAccessKey);
    }

    private static ChannelDetailsViewModel CreateTestChannel(
        Guid id,
        Guid workspaceId,
        string name = "Test Channel",
        string channelType = "Email")
    {
        return new ChannelDetailsViewModel
        {
            Id = id,
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace",
            Name = name,
            ChannelType = channelType,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            AlertCount = 0,
            EventSubscriptionCount = 0,
            MaskedConfiguration = [],
            ConfigurationJson = []
        };
    }

    private static ChannelDetailsViewModel CreateTestEventGridChannel(
        Guid id,
        Guid workspaceId,
        string? accessKey)
    {
        var config = new Dictionary<string, System.Text.Json.JsonElement>
        {
            [ConfigurationKeys.EventGrid.TopicEndpoint] = System.Text.Json.JsonSerializer.SerializeToElement("https://test.eventgrid.azure.net/api/events")
        };

        if (accessKey != null)
        {
            config[ConfigurationKeys.EventGrid.AccessKey] = System.Text.Json.JsonSerializer.SerializeToElement(accessKey);
        }

        return new ChannelDetailsViewModel
        {
            Id = id,
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace",
            Name = "EventGrid Channel",
            ChannelType = SAMA.Web.Constants.ChannelTypes.EventGrid,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            AlertCount = 0,
            EventSubscriptionCount = 0,
            MaskedConfiguration = [],
            ConfigurationJson = config
        };
    }
}
