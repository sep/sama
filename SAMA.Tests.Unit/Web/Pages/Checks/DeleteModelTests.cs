using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Checks;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Checks;

[TestClass]
public class DeleteModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private CheckQueryService _mockCheckQuery = null!;
    private CheckCommandService _mockCheckCommand = null!;
    private DeleteModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockCheckQuery = Substitute.For<CheckQueryService>(null!, null!, null!, null!);
        _mockCheckCommand = Substitute.For<CheckCommandService>(null!, null!, null!, null!, null!);
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);

        _pageModel = new DeleteModel(_mockWorkspaceQuery, _mockCheckQuery, _mockCheckCommand);
        PageModelTestHelpers.ConfigurePageModel(_pageModel);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenCheckDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(null));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenCheckExists()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = "http",
            Schedule = "60",
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            ResultCount = 100,
            AlertCount = 2
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateCheckToDeleteProperty()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var createdAt = DateTimeOffset.UtcNow.AddDays(-7);
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Check to Delete",
            CheckType = "tcp",
            Schedule = "300",
            Enabled = false,
            CreatedAt = createdAt,
            ResultCount = 50,
            AlertCount = 3
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(checkId);

        Assert.AreEqual(checkId, _pageModel.CheckToDelete.Id);
        Assert.AreEqual("Check to Delete", _pageModel.CheckToDelete.Name);
        Assert.AreEqual("tcp", _pageModel.CheckToDelete.CheckType);
        Assert.AreEqual("300", _pageModel.CheckToDelete.Schedule);
        Assert.IsFalse(_pageModel.CheckToDelete.Enabled);
        Assert.AreEqual(createdAt, _pageModel.CheckToDelete.CreatedAt);
        Assert.AreEqual(50, _pageModel.CheckToDelete.ResultCount);
        Assert.AreEqual(3, _pageModel.CheckToDelete.AlertCount);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallGetCheckDetailsAsync()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(checkId);

        await _mockCheckQuery.Received(1).GetCheckDetailsAsync(checkId);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnPostAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenCheckDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(null));

        var result = await _pageModel.OnPostAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallDeleteCheckAsync()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Check to Delete"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockCheckCommand.DeleteCheckAsync(checkId, Arg.Any<string>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(checkId);

        await _mockCheckCommand.Received(1).DeleteCheckAsync(checkId, Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulDeletion()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Check to Delete"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockCheckCommand.DeleteCheckAsync(checkId, Arg.Any<string>()).Returns(Task.FromResult(true));

        var result = await _pageModel.OnPostAsync(checkId);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.AreEqual(workspaceId, redirect.RouteValues?["workspaceId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageInTempData()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Deleted Check"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockCheckCommand.DeleteCheckAsync(checkId, Arg.Any<string>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(checkId);

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "Deleted Check");
        StringAssert.Contains(message, "deleted successfully");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnBadRequestWhenDeleteCheckAsyncReturnsFalse()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Check to Delete"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockCheckCommand.DeleteCheckAsync(checkId, Arg.Any<string>()).Returns(Task.FromResult(false));

        var result = await _pageModel.OnPostAsync(checkId);

        Assert.IsInstanceOfType<BadRequestResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallDeleteCheckAsyncWhenCheckDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(null));

        await _pageModel.OnPostAsync(checkId);

        await _mockCheckCommand.DidNotReceive().DeleteCheckAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldPreserveCheckNameForSuccessMessage()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Important Check"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockCheckCommand.DeleteCheckAsync(checkId, Arg.Any<string>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(checkId);

        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.Contains("Important Check", message!);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldPreserveWorkspaceIdForRedirect()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Check to Delete"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockCheckCommand.DeleteCheckAsync(checkId, Arg.Any<string>()).Returns(Task.FromResult(true));

        var result = await _pageModel.OnPostAsync(checkId);

        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual(workspaceId, redirect.RouteValues?["workspaceId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotSetSuccessMessageWhenDeletionFails()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Check to Delete"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockCheckCommand.DeleteCheckAsync(checkId, Arg.Any<string>()).Returns(Task.FromResult(false));

        await _pageModel.OnPostAsync(checkId);

        Assert.IsFalse(_pageModel.TempData.ContainsKey("SuccessMessage"));
    }

    [TestMethod]
    public async Task OnGetAsyncShouldMapAllCheckDetailsToViewModel()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var createdAt = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Complete Check",
            CheckType = "dns",
            Schedule = "600",
            Enabled = true,
            CreatedAt = createdAt,
            ResultCount = 999,
            AlertCount = 5
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(checkId);

        Assert.AreEqual(checkId, _pageModel.CheckToDelete.Id);
        Assert.AreEqual("Complete Check", _pageModel.CheckToDelete.Name);
        Assert.AreEqual("dns", _pageModel.CheckToDelete.CheckType);
        Assert.AreEqual("600", _pageModel.CheckToDelete.Schedule);
        Assert.IsTrue(_pageModel.CheckToDelete.Enabled);
        Assert.AreEqual(createdAt, _pageModel.CheckToDelete.CreatedAt);
        Assert.AreEqual(999, _pageModel.CheckToDelete.ResultCount);
        Assert.AreEqual(5, _pageModel.CheckToDelete.AlertCount);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallGetCheckDetailsAsyncBeforeDeletion()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Check to Delete"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockCheckCommand.DeleteCheckAsync(checkId, Arg.Any<string>()).Returns(Task.FromResult(true));

        await _pageModel.OnPostAsync(checkId);

        await _mockCheckQuery.Received(1).GetCheckDetailsAsync(checkId);
        await _mockCheckCommand.Received(1).DeleteCheckAsync(checkId, Arg.Any<string>());
    }
}
