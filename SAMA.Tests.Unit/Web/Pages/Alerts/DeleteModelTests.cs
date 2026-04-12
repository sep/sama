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
public class DeleteModelTests
{
    private AlertQueryService _mockAlertQuery = null!;
    private AlertCommandService _mockAlertCommand = null!;
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private DeleteModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockAlertQuery = Substitute.For<AlertQueryService>((SamaDbContext)null!);
        _mockAlertCommand = Substitute.For<AlertCommandService>(null!, null!, null!, null!, null!, null!);
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);

        _pageModel = new DeleteModel(_mockWorkspaceQuery, _mockAlertQuery, _mockAlertCommand);
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
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId, checkName: "Critical Check");
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        Assert.AreEqual(checkId, _pageModel.CheckId);
        Assert.AreEqual("Critical Check", _pageModel.CheckName);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateAlertToDelete()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = new AlertDetailsViewModel
        {
            Id = alertId,
            CheckId = checkId,
            CheckName = "Test Check",
            WorkspaceId = workspaceId,
            WorkspaceName = "Test Workspace",
            Name = "Alert to Delete",
            TriggerOnWarn = true,
            TriggerOnDown = false,
            FailureThreshold = 5,
            SendRecoveryNotification = true,
            Enabled = false,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            Channels = [],
            AlertHistoryCount = 25
        };
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        Assert.AreEqual(alertId, _pageModel.AlertToDelete.Id);
        Assert.AreEqual("Alert to Delete", _pageModel.AlertToDelete.Name);
        Assert.IsTrue(_pageModel.AlertToDelete.TriggerOnWarn);
        Assert.IsFalse(_pageModel.AlertToDelete.TriggerOnDown);
        Assert.AreEqual(5, _pageModel.AlertToDelete.FailureThreshold);
        Assert.IsFalse(_pageModel.AlertToDelete.Enabled);
        Assert.AreEqual(25, _pageModel.AlertToDelete.AlertHistoryCount);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnPostAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenAlertDoesNotExist()
    {
        var alertId = Guid.NewGuid();
        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(null));

        var result = await _pageModel.OnPostAsync(alertId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallDeleteAlertAsyncWithCorrectId()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId);

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockAlertCommand.DeleteAlertAsync(alertId, Arg.Any<string>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(alertId);

        await _mockAlertCommand.Received(1).DeleteAlertAsync(alertId, Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulDeletion()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId);

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockAlertCommand.DeleteAlertAsync(alertId, Arg.Any<string>()).Returns(Task.FromResult(true));

        var result = await _pageModel.OnPostAsync(alertId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.AreEqual(checkId, redirect.RouteValues?["checkId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageAfterSuccessfulDeletion()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId, alertName: "Production Alert");

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockAlertCommand.DeleteAlertAsync(alertId, Arg.Any<string>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(alertId);

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "Production Alert");
        StringAssert.Contains(message, "deleted successfully");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetErrorMessageWhenDeletionFails()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId);

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockAlertCommand.DeleteAlertAsync(alertId, Arg.Any<string>()).Returns(Task.FromResult(false));

        await _pageModel.OnPostAsync(alertId);

        Assert.IsTrue(_pageModel.TempData.ContainsKey("ErrorMessage"));
        var message = _pageModel.TempData["ErrorMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "Failed to delete alert");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexEvenWhenDeletionFails()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId);

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockAlertCommand.DeleteAlertAsync(alertId, Arg.Any<string>()).Returns(Task.FromResult(false));

        var result = await _pageModel.OnPostAsync(alertId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.AreEqual(checkId, redirect.RouteValues?["checkId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallDeleteAlertAsyncWhenAlertDoesNotExist()
    {
        var alertId = Guid.NewGuid();
        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(null));

        await _pageModel.OnPostAsync(alertId);

        await _mockAlertCommand.DidNotReceive().DeleteAlertAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnGetAsyncShouldLoadWorkspaceContextWithCorrectParameters()
    {
        var alertId = Guid.NewGuid();
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var alert = CreateTestAlertDetailsViewModel(alertId, checkId, workspaceId, workspaceName: "Production");
        var workspace = new Workspace { Id = workspaceId, Name = "Production" };

        _mockAlertQuery.GetAlertDetailsAsync(alertId).Returns(Task.FromResult<AlertDetailsViewModel?>(alert));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(alertId);

        await _mockWorkspaceQuery.Received(1).GetWorkspaceByIdAsync(workspaceId);
        Assert.AreEqual(workspaceId, _pageModel.WorkspaceId);
        Assert.AreEqual("Production", _pageModel.WorkspaceName);
    }

    private static AlertDetailsViewModel CreateTestAlertDetailsViewModel(
        Guid alertId,
        Guid checkId,
        Guid workspaceId,
        string alertName = "Test Alert",
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
            Name = alertName,
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
