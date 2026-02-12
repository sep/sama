using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Alerts;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Alerts;

[TestClass]
public class EditModelTests
{
    private CheckQueryService _mockCheckQuery = null!;
    private ChannelQueryService _mockChannelQuery = null!;
    private AlertQueryService _mockAlertQuery = null!;
    private AlertCommandService _mockAlertCommand = null!;
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private EditModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockCheckQuery = Substitute.For<CheckQueryService>(null!, null!, null!, null!);
        _mockChannelQuery = Substitute.For<ChannelQueryService>(null!, null!);
        _mockAlertQuery = Substitute.For<AlertQueryService>((SamaDbContext)null!);
        _mockAlertCommand = Substitute.For<AlertCommandService>(null!, null!, null!, null!, null!);
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>((SamaDbContext)null!);

        _pageModel = new EditModel(_mockWorkspaceQuery, _mockChannelQuery, _mockCheckQuery, _mockAlertQuery, _mockAlertCommand);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenAlertDoesNotExist()
    {
        var alertId = Guid.NewGuid();
        _mockAlertQuery.GetAlertForEditAsync(alertId).Returns(Task.FromResult<AlertEditViewModel?>(null));

        var result = await _pageModel.OnGetAsync(alertId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenAlertExists()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = new AlertEditViewModel
        {
            Id = alertId,
            CheckId = checkId,
            CheckName = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace",
            Name = "Test Alert",
            TriggerOnWarn = true,
            TriggerOnDown = false,
            FailureThreshold = 2,
            SendRecoveryNotification = false,
            Enabled = true,
            SelectedChannelIds = [Guid.NewGuid()]
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertForEditAsync(alertId).Returns(Task.FromResult<AlertEditViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.ChannelListItemViewModel>()));

        var result = await _pageModel.OnGetAsync(alertId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateInputFromAlert()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var alert = new AlertEditViewModel
        {
            Id = alertId,
            CheckId = checkId,
            CheckName = "My Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace",
            Name = "My Alert",
            TriggerOnWarn = true,
            TriggerOnDown = false,
            FailureThreshold = 3,
            SendRecoveryNotification = false,
            Enabled = true,
            SelectedChannelIds = [channelId]
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertForEditAsync(alertId).Returns(Task.FromResult<AlertEditViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<SAMA.Web.Models.ChannelListItemViewModel>()));

        await _pageModel.OnGetAsync(alertId);

        Assert.AreEqual(checkId, _pageModel.CheckId);
        Assert.AreEqual("My Check", _pageModel.CheckName);
        Assert.AreEqual(alertId, _pageModel.Input.Id);
        Assert.AreEqual(checkId, _pageModel.Input.CheckId);
        Assert.AreEqual("My Alert", _pageModel.Input.Name);
        Assert.IsTrue(_pageModel.Input.TriggerOnWarn);
        Assert.IsFalse(_pageModel.Input.TriggerOnDown);
        Assert.AreEqual(3, _pageModel.Input.FailureThreshold);
        Assert.IsFalse(_pageModel.Input.SendRecoveryNotification);
        Assert.IsTrue(_pageModel.Input.Enabled);
        Assert.HasCount(1, _pageModel.Input.SelectedChannelIds);
        Assert.AreEqual(channelId, _pageModel.Input.SelectedChannelIds[0]);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateAvailableChannels()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = new AlertEditViewModel
        {
            Id = alertId,
            CheckId = checkId,
            CheckName = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace",
            Name = "Test Alert",
            TriggerOnDown = true,
            FailureThreshold = 1,
            SendRecoveryNotification = true,
            Enabled = true,
            SelectedChannelIds = []
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var channels = new List<ChannelListItemViewModel>
        {
            new() { Id = Guid.NewGuid(), Name = "Channel 1", ChannelType = "Email", Enabled = true },
            new() { Id = Guid.NewGuid(), Name = "Channel 2", ChannelType = "Slack", Enabled = true }
        };

        _mockAlertQuery.GetAlertForEditAsync(alertId).Returns(Task.FromResult<AlertEditViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(channels));

        await _pageModel.OnGetAsync(alertId);

        Assert.HasCount(2, _pageModel.AvailableChannels);
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
            .Returns(Task.FromResult(new List<ChannelListItemViewModel>()));

        _pageModel.Input = new EditModel.InputModel { CheckId = checkId };
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
            .Returns(Task.FromResult(new List<ChannelListItemViewModel>()));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = Guid.NewGuid(),
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
    public async Task OnPostAsyncShouldCallUpdateAlertAsyncWithCorrectParameters()
    {
        var alertId = Guid.NewGuid();
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
        var updateResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            ShouldTriggerCheck = false,
            ChannelCount = 2,
            AllChannelsCount = 0
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.UpdateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(updateResult));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = alertId,
            CheckId = checkId,
            Name = "Updated Alert",
            TriggerOnWarn = true,
            TriggerOnDown = false,
            FailureThreshold = 5,
            SendRecoveryNotification = false,
            Enabled = true,
            SelectedChannelIds = [Guid.NewGuid(), Guid.NewGuid()]
        };

        await _pageModel.OnPostAsync();

        await _mockAlertCommand.Received(1).UpdateAlertAsync(
            alertId,
            "Updated Alert",
            true,
            false,
            5,
            false,
            true,
            Arg.Is<List<Guid>>(ids => ids.Count == 2),
            Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulUpdate()
    {
        var alertId = Guid.NewGuid();
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
        var updateResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            ShouldTriggerCheck = false,
            ChannelCount = 1,
            AllChannelsCount = 0
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.UpdateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(updateResult));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = alertId,
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
        var alertId = Guid.NewGuid();
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
        var updateResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            ShouldTriggerCheck = true,
            ChannelCount = 3,
            AllChannelsCount = 0
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.UpdateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(updateResult));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = alertId,
            CheckId = checkId,
            Name = "Immediate Alert",
            TriggerOnDown = true,
            FailureThreshold = 1
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        Assert.Contains("Immediate Alert", message);
        Assert.Contains("updated successfully", message);
        Assert.Contains("3 channel(s)", message);
        Assert.Contains("executed immediately", message);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageWhenCheckIsNotTriggered()
    {
        var alertId = Guid.NewGuid();
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
        var updateResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            ShouldTriggerCheck = false,
            ChannelCount = 1,
            AllChannelsCount = 0
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.UpdateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(updateResult));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = alertId,
            CheckId = checkId,
            Name = "Regular Alert",
            TriggerOnDown = true,
            FailureThreshold = 1
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        Assert.Contains("Regular Alert", message);
        Assert.Contains("updated successfully", message);
        Assert.Contains("1 channel(s)", message);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageWithAllChannelsWhenNoChannelsSelected()
    {
        var alertId = Guid.NewGuid();
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
        var updateResult = new CreateUpdateAlertResultViewModel
        {
            Success = true,
            ShouldTriggerCheck = false,
            ChannelCount = 0,
            AllChannelsCount = 4
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockAlertCommand.UpdateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(updateResult));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = alertId,
            CheckId = checkId,
            Name = "All Channels Alert",
            TriggerOnDown = true,
            FailureThreshold = 1,
            SelectedChannelIds = []
        };

        await _pageModel.OnPostAsync();

        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        Assert.Contains("all workspace channels", message);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenUpdateAlertAsyncFails()
    {
        var alertId = Guid.NewGuid();
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
        var updateResult = new CreateUpdateAlertResultViewModel
        {
            Success = false,
            ErrorMessage = "Database error"
        };

        _mockCheckQuery.GetCheckBasicInfoAsync(checkId).Returns(Task.FromResult<CheckBasicInfoViewModel?>(check));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockChannelQuery.GetChannelsForWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<ChannelListItemViewModel>()));
        _mockAlertCommand.UpdateAlertAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<int>(),
            Arg.Any<bool>(),
            Arg.Any<bool>(),
            Arg.Any<List<Guid>>(),
            Arg.Any<string>()).Returns(Task.FromResult(updateResult));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = alertId,
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

        _pageModel.Input = new EditModel.InputModel
        {
            Id = Guid.NewGuid(),
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
    public async Task OnPostAsyncShouldNotCallUpdateAlertAsyncWhenModelStateIsInvalid()
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
            .Returns(Task.FromResult(new List<ChannelListItemViewModel>()));

        _pageModel.Input = new EditModel.InputModel { CheckId = checkId };
        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        await _pageModel.OnPostAsync();

        await _mockAlertCommand.DidNotReceive().UpdateAlertAsync(
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
