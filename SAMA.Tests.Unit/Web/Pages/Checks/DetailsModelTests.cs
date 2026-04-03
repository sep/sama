using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Checks;
using SAMA.Web.Services;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Checks;

[TestClass]
public class DetailsModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private CheckQueryService _mockCheckQuery = null!;
    private ScriptOutputBuffer _mockScriptOutputBuffer = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private DetailsModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockCheckQuery = Substitute.For<CheckQueryService>(null!, null!, null!, null!);
        _mockScriptOutputBuffer = new ScriptOutputBuffer(Substitute.For<ILogger<ScriptOutputBuffer>>());
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!, null!, null!);
        _mockGlobalSettings.DashboardRefreshIntervalSeconds.Returns(30);

        _pageModel = new DetailsModel(_mockWorkspaceQuery, _mockCheckQuery, _mockScriptOutputBuffer, _mockGlobalSettings);
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
            CheckType = "http"
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateCheckProperty()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checkDetails = new CheckDetailsViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "My Check",
            Description = "My Description",
            CheckType = "tcp",
            Schedule = "120",
            TimeoutSeconds = 45,
            Enabled = true,
            ResultCount = 100,
            AlertCount = 2
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(checkId);

        Assert.AreEqual(checkId, _pageModel.Check.Id);
        Assert.AreEqual("My Check", _pageModel.Check.Name);
        Assert.AreEqual("My Description", _pageModel.Check.Description);
        Assert.AreEqual("tcp", _pageModel.Check.CheckType);
        Assert.AreEqual("120", _pageModel.Check.Schedule);
        Assert.AreEqual(45, _pageModel.Check.TimeoutSeconds);
        Assert.IsTrue(_pageModel.Check.Enabled);
        Assert.AreEqual(100, _pageModel.Check.ResultCount);
        Assert.AreEqual(2, _pageModel.Check.AlertCount);
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
    public async Task OnGetHistoryAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnGetHistoryAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetHistoryAsyncShouldReturnNotFoundWhenCheckDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(null));

        var result = await _pageModel.OnGetHistoryAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetHistoryAsyncShouldReturnJsonResultWithHistoryData()
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
        var history = new List<CheckHistoryItemViewModel>
        {
            new() { Status = "up", Timestamp = DateTimeOffset.UtcNow, ResponseTimeMs = 100 },
            new() { Status = "down", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5), ErrorMessage = "Connection failed" }
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetCheckHistoryAsync(checkId, 24).Returns(Task.FromResult(history));

        var result = await _pageModel.OnGetHistoryAsync(checkId);

        Assert.IsInstanceOfType<JsonResult>(result);
        var jsonResult = (JsonResult)result;
        Assert.AreEqual(history, jsonResult.Value);
    }

    [TestMethod]
    public async Task OnGetHistoryAsyncShouldUseDefaultHoursParameter()
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
        var history = new List<CheckHistoryItemViewModel>();

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetCheckHistoryAsync(checkId, 24).Returns(Task.FromResult(history));

        await _pageModel.OnGetHistoryAsync(checkId);

        await _mockCheckQuery.Received(1).GetCheckHistoryAsync(checkId, 24);
    }

    [TestMethod]
    public async Task OnGetHistoryAsyncShouldUseCustomHoursParameter()
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
        var history = new List<CheckHistoryItemViewModel>();

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetCheckHistoryAsync(checkId, 48).Returns(Task.FromResult(history));

        await _pageModel.OnGetHistoryAsync(checkId, 48);

        await _mockCheckQuery.Received(1).GetCheckHistoryAsync(checkId, 48);
    }

    [TestMethod]
    public async Task OnGetHistoryAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
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

        var result = await _pageModel.OnGetHistoryAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetUptimeAsyncShouldReturnNotFoundWhenIdIsNull()
    {
        var result = await _pageModel.OnGetUptimeAsync(null);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetUptimeAsyncShouldReturnNotFoundWhenCheckDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(null));

        var result = await _pageModel.OnGetUptimeAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetUptimeAsyncShouldReturnJsonResultWithUptimeData()
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
        var uptime = new CheckUptimeViewModel
        {
            UptimePercentage = 99.5,
            TotalChecks = 1000,
            UpCount = 995,
            WarnCount = 3,
            DownCount = 2
        };

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetCheckUptimeAsync(checkId, 24).Returns(Task.FromResult<CheckUptimeViewModel?>(uptime));

        var result = await _pageModel.OnGetUptimeAsync(checkId);

        Assert.IsInstanceOfType<JsonResult>(result);
        var jsonResult = (JsonResult)result;
        Assert.AreEqual(uptime, jsonResult.Value);
    }

    [TestMethod]
    public async Task OnGetUptimeAsyncShouldReturnDefaultUptimeWhenNull()
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
        _mockCheckQuery.GetCheckUptimeAsync(checkId, 24).Returns(Task.FromResult<CheckUptimeViewModel?>(null));

        var result = await _pageModel.OnGetUptimeAsync(checkId);

        Assert.IsInstanceOfType<JsonResult>(result);
        var jsonResult = (JsonResult)result;
        Assert.IsNotNull(jsonResult.Value);

        var anonType = jsonResult.Value;
        var uptimePercentage = anonType.GetType().GetProperty("UptimePercentage")?.GetValue(anonType);
        var totalChecks = anonType.GetType().GetProperty("TotalChecks")?.GetValue(anonType);
        var upCount = anonType.GetType().GetProperty("UpCount")?.GetValue(anonType);
        var warnCount = anonType.GetType().GetProperty("WarnCount")?.GetValue(anonType);
        var downCount = anonType.GetType().GetProperty("DownCount")?.GetValue(anonType);

        Assert.AreEqual(0.0, uptimePercentage);
        Assert.AreEqual(0, totalChecks);
        Assert.AreEqual(0, upCount);
        Assert.AreEqual(0, warnCount);
        Assert.AreEqual(0, downCount);
    }

    [TestMethod]
    public async Task OnGetUptimeAsyncShouldUseDefaultHoursParameter()
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
        var uptime = new CheckUptimeViewModel();

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetCheckUptimeAsync(checkId, 24).Returns(Task.FromResult<CheckUptimeViewModel?>(uptime));

        await _pageModel.OnGetUptimeAsync(checkId);

        await _mockCheckQuery.Received(1).GetCheckUptimeAsync(checkId, 24);
    }

    [TestMethod]
    public async Task OnGetUptimeAsyncShouldUseCustomHoursParameter()
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
        var uptime = new CheckUptimeViewModel();

        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(checkDetails));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckQuery.GetCheckUptimeAsync(checkId, 72).Returns(Task.FromResult<CheckUptimeViewModel?>(uptime));

        await _pageModel.OnGetUptimeAsync(checkId, 72);

        await _mockCheckQuery.Received(1).GetCheckUptimeAsync(checkId, 72);
    }

    [TestMethod]
    public async Task OnGetUptimeAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
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

        var result = await _pageModel.OnGetUptimeAsync(checkId);

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
    public async Task OnGetHistoryAsyncShouldNotCallGetCheckHistoryWhenCheckNotFound()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(null));

        await _pageModel.OnGetHistoryAsync(checkId);

        await _mockCheckQuery.DidNotReceive().GetCheckHistoryAsync(Arg.Any<Guid>(), Arg.Any<int>());
    }

    [TestMethod]
    public async Task OnGetUptimeAsyncShouldNotCallGetCheckUptimeWhenCheckNotFound()
    {
        var checkId = Guid.NewGuid();
        _mockCheckQuery.GetCheckDetailsAsync(checkId).Returns(Task.FromResult<CheckDetailsViewModel?>(null));

        await _pageModel.OnGetUptimeAsync(checkId);

        await _mockCheckQuery.DidNotReceive().GetCheckUptimeAsync(Arg.Any<Guid>(), Arg.Any<int>());
    }
}
