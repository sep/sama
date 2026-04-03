using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Alerts;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Alerts;

[TestClass]
public class DetailsModelTests
{
    private AlertQueryService _mockAlertQuery = null!;
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private DetailsModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockAlertQuery = Substitute.For<AlertQueryService>((SamaDbContext)null!);
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);

        _pageModel = new DetailsModel(_mockWorkspaceQuery, _mockAlertQuery);
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
        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(null));

        var result = await _pageModel.OnGetAsync(alertId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenAlertExists()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId);
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(alertId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateCheckIdAndName()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId, checkName: "My Test Check");
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        Assert.AreEqual(checkId, _pageModel.CheckId);
        Assert.AreEqual("My Test Check", _pageModel.CheckName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateAlertDetails()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddDays(-5);
        var updatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var alert = new AlertDetailsViewModel
        {
            Id = alertId,
            CheckId = checkId,
            CheckName = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace",
            Name = "Critical Alert",
            TriggerOnWarn = true,
            TriggerOnDown = false,
            FailureThreshold = 3,
            SendRecoveryNotification = false,
            Enabled = true,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            Channels = [
                new AlertDetailsViewModel.ChannelInfo
                {
                    Id = Guid.NewGuid(),
                    Name = "Email Channel",
                    ChannelType = "Email",
                    Enabled = true
                },
                new AlertDetailsViewModel.ChannelInfo
                {
                    Id = Guid.NewGuid(),
                    Name = "Slack Channel",
                    ChannelType = "Slack",
                    Enabled = false
                }
            ],
            AlertHistoryCount = 42
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        Assert.AreEqual(alertId, _pageModel.Alert.Id);
        Assert.AreEqual("Critical Alert", _pageModel.Alert.Name);
        Assert.IsTrue(_pageModel.Alert.TriggerOnWarn);
        Assert.IsFalse(_pageModel.Alert.TriggerOnDown);
        Assert.AreEqual(3, _pageModel.Alert.FailureThreshold);
        Assert.IsFalse(_pageModel.Alert.SendRecoveryNotification);
        Assert.IsTrue(_pageModel.Alert.Enabled);
        Assert.AreEqual(createdAt, _pageModel.Alert.CreatedAt);
        Assert.AreEqual(updatedAt, _pageModel.Alert.UpdatedAt);
        Assert.HasCount(2, _pageModel.Alert.Channels);
        Assert.AreEqual(42, _pageModel.Alert.AlertHistoryCount);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateChannelsList()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId);
        alert.Channels =
        [
            new AlertDetailsViewModel.ChannelInfo
            {
                Id = Guid.NewGuid(),
                Name = "Channel 1",
                ChannelType = "Email",
                Enabled = true
            },
            new AlertDetailsViewModel.ChannelInfo
            {
                Id = Guid.NewGuid(),
                Name = "Channel 2",
                ChannelType = "Slack",
                Enabled = false
            },
            new AlertDetailsViewModel.ChannelInfo
            {
                Id = Guid.NewGuid(),
                Name = "Channel 3",
                ChannelType = "Teams",
                Enabled = true
            }
        ];
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        Assert.HasCount(3, _pageModel.Alert.Channels);
        Assert.AreEqual("Channel 1", _pageModel.Alert.Channels[0].Name);
        Assert.AreEqual("Email", _pageModel.Alert.Channels[0].ChannelType);
        Assert.IsTrue(_pageModel.Alert.Channels[0].Enabled);
        Assert.AreEqual("Channel 2", _pageModel.Alert.Channels[1].Name);
        Assert.IsFalse(_pageModel.Alert.Channels[1].Enabled);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateEmptyChannelsListWhenNoChannels()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId);
        alert.Channels = [];
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        Assert.IsEmpty(_pageModel.Alert.Channels);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetAlertDetailsAsyncWithCorrectId()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId);
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        await _mockAlertQuery.Received(1).GetAlertDetailsAsync(alertId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadWorkspaceContextWithCorrectParameters()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId, workspaceName: "My Workspace");
        var workspace = new Workspace { Id = workspaceId, Name = "My Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        await _mockWorkspaceQuery.Received(1).GetWorkspaceByIdAsync(workspaceId);
        Assert.AreEqual(workspaceId, _pageModel.WorkspaceId);
        Assert.AreEqual("My Workspace", _pageModel.WorkspaceName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldNotLoadWorkspaceContextWhenAlertDoesNotExist()
    {
        var alertId = Guid.NewGuid();
        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(null));

        await _pageModel.OnGetAsync(alertId);

        await _mockWorkspaceQuery.DidNotReceive().GetWorkspaceByIdAsync(Arg.Any<Guid>());
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateAlertHistoryCount()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId);
        alert.AlertHistoryCount = 15;
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        Assert.AreEqual(15, _pageModel.Alert.AlertHistoryCount);
    }

    private static AlertDetailsViewModel CreateTestAlertDetailsViewModel(
        Guid alertId,
        Guid checkId,
        Guid workspaceId,
        string checkName = "Test Check",
        string workspaceName = "Test Workspace")
    {
        return new AlertDetailsViewModel
        {
            Id = alertId,
            CheckId = checkId,
            CheckName = checkName,
            WorkspaceId = workspaceId,
            WorkspaceName = workspaceName,
            Name = "Test Alert",
            TriggerOnWarn = false,
            TriggerOnDown = true,
            FailureThreshold = 1,
            SendRecoveryNotification = true,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
            UpdatedAt = DateTimeOffset.UtcNow,
            Channels = [],
            AlertHistoryCount = 0
        };
    }
}
