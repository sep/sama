using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Alerts;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Alerts;

[TestClass]
public class CreateModelTests
{
    private CheckQueryService _mockCheckQuery = null!;
    private ChannelQueryService _mockChannelQuery = null!;
    private AlertCommandService _mockAlertCommand = null!;
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private CreateModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockCheckQuery = Substitute.For<CheckQueryService>(null!, null!, null!, null!);
        _mockChannelQuery = Substitute.For<ChannelQueryService>(null!, null!);
        _mockAlertCommand = Substitute.For<AlertCommandService>(null!, null!, null!, null!, null!, null!);
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);

        _pageModel = new CreateModel(_mockWorkspaceQuery, _mockChannelQuery, _mockCheckQuery, _mockAlertCommand);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnRedirectWhenCheckIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Workspaces/Index", redirect.PageName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenCheckDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(null));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenCheckExists()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.ChannelListItemViewModel>()));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateCheckIdAndName()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "My Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.ChannelListItemViewModel>()));

        await _pageModel.OnGetAsync(checkId);

        Assert.AreEqual(checkId, _pageModel.CheckId);
        Assert.AreEqual("My Test Check", _pageModel.CheckName);
        Assert.AreEqual(checkId, _pageModel.Input.CheckId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateAvailableChannels()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var channels = new List<ChannelListItemViewModel>
        {
            new() { Id = Guid.NewGuid(), Name = "Channel 1", ChannelType = "Email", Enabled = true },
            new() { Id = Guid.NewGuid(), Name = "Channel 2", ChannelType = "Slack", Enabled = true }
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channels));

        await _pageModel.OnGetAsync(checkId);

        Assert.HasCount(2, _pageModel.AvailableChannels);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenCheckDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _pageModel.Input.CheckId = checkId;

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(null));

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenModelStateIsInvalid()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.ChannelListItemViewModel>()));

        _pageModel.Input.CheckId = checkId;
        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenNeitherTriggerOnWarnNorTriggerOnDownIsSet()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.ChannelListItemViewModel>()));

        _pageModel.Input = new CreateModel.InputModel
        {
            CheckId = checkId,
            Name = "Test Alert",
            TriggerOnWarn = false,
            TriggerOnDown = false,
            FailureThreshold = 1
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
        Assert.IsTrue(_pageModel.ModelState.ContainsKey(string.Empty));
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallCreateAlertAsyncWithCorrectParameters()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var createResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            AlertId = Guid.NewGuid(),
            ShouldTriggerCheck = false,
            ChannelCount = 2,
            AllChannelsCount = 0
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.CreateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(createResult));

        _pageModel.Input = new CreateModel.InputModel
        {
            CheckId = checkId,
            Name = "My Alert",
            TriggerOnWarn = true,
            TriggerOnDown = false,
            FailureThreshold = 3,
            SendRecoveryNotification = false,
            Enabled = true,
            SelectedChannelIds = [Guid.NewGuid(), Guid.NewGuid()]
        };

        await _pageModel.OnPostAsync();

        await _mockAlertCommand.Received(1).CreateAlertAsync(
            checkId,
            "My Alert",
            true,
            false,
            3,
            false,
            true,
            Arg.Is<List<Guid>>(ids => ids.Count == 2),
            Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulCreation()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var createResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            AlertId = Guid.NewGuid(),
            ShouldTriggerCheck = false,
            ChannelCount = 1,
            AllChannelsCount = 0
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.CreateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(createResult));

        _pageModel.Input = new CreateModel.InputModel
        {
            CheckId = checkId,
            Name = "Test Alert",
            TriggerOnDown = true,
            FailureThreshold = 1
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.AreEqual(checkId, redirect.RouteValues?["checkId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageWhenCheckIsTriggered()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var createResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            AlertId = Guid.NewGuid(),
            ShouldTriggerCheck = true,
            ChannelCount = 2,
            AllChannelsCount = 0
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.CreateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(createResult));

        _pageModel.Input = new CreateModel.InputModel
        {
            CheckId = checkId,
            Name = "Immediate Alert",
            TriggerOnDown = true,
            FailureThreshold = 1
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "Immediate Alert");
        StringAssert.Contains(message, "created successfully");
        StringAssert.Contains(message, "2 channel(s)");
        StringAssert.Contains(message, "executed immediately");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageWhenCheckIsNotTriggered()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var createResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            AlertId = Guid.NewGuid(),
            ShouldTriggerCheck = false,
            ChannelCount = 1,
            AllChannelsCount = 0
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.CreateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(createResult));

        _pageModel.Input = new CreateModel.InputModel
        {
            CheckId = checkId,
            Name = "Regular Alert",
            TriggerOnDown = true,
            FailureThreshold = 1
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "Regular Alert");
        StringAssert.Contains(message, "created successfully");
        StringAssert.Contains(message, "1 channel(s)");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageWithAllChannelsWhenNoChannelsSelected()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var createResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            AlertId = Guid.NewGuid(),
            ShouldTriggerCheck = true,
            ChannelCount = 0,
            AllChannelsCount = 5
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.CreateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(createResult));

        _pageModel.Input = new CreateModel.InputModel
        {
            CheckId = checkId,
            Name = "All Channels Alert",
            TriggerOnDown = true,
            FailureThreshold = 1,
            SelectedChannelIds = []
        };

        await _pageModel.OnPostAsync();

        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "all 5 workspace channel(s)");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenCreateAlertAsyncFails()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var createResult = new CreateUpdateAlertResultViewModel
        {
            Success = false,
            ErrorMessage = "Database error"
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.ChannelListItemViewModel>()));
        _mockAlertCommand.CreateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(createResult));

        _pageModel.Input = new CreateModel.InputModel
        {
            CheckId = checkId,
            Name = "Failed Alert",
            TriggerOnDown = true,
            FailureThreshold = 1
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
        Assert.IsTrue(_pageModel.ModelState.ContainsKey(string.Empty));
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRepopulateCheckIdAndNameWhenReturningPage()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check Name",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.ChannelListItemViewModel>()));

        _pageModel.Input = new CreateModel.InputModel
        {
            CheckId = checkId,
            Name = "Test Alert",
            TriggerOnWarn = false,
            TriggerOnDown = false,
            FailureThreshold = 1
        };

        await _pageModel.OnPostAsync();

        Assert.AreEqual(checkId, _pageModel.CheckId);
        Assert.AreEqual("Test Check Name", _pageModel.CheckName);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallCreateAlertAsyncWhenModelStateIsInvalid()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var check = new CheckBasicInfoViewModel
        {
            Id = checkId,
            Name = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace"
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.ChannelListItemViewModel>()));

        _pageModel.Input.CheckId = checkId;
        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        await _pageModel.OnPostAsync();

        await _mockAlertCommand.DidNotReceive().CreateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>());
    }
}
