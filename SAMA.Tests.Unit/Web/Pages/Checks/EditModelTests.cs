using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Models;
using SAMA.Web.Pages.Checks;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Checks;

[TestClass]
public class EditModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private CheckQueryService _mockCheckQuery = null!;
    private CheckConfigurationService _mockConfigService = null!;
    private CheckCommandService _mockCheckCommand = null!;
    private EditModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockConfigService = Substitute.For<CheckConfigurationService>();
        _mockCheckQuery = Substitute.For<CheckQueryService>(null!, null!, null!, null!);
        _mockCheckCommand = Substitute.For<CheckCommandService>(null!, null!, null!, null!, null!, null!);

        _pageModel = new EditModel(_mockWorkspaceQuery, _mockCheckQuery, _mockConfigService, _mockCheckCommand);
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
        _mockCheckQuery.GetCheckForEditAsync(checkId).Returns(Task.FromResult<CheckEditViewModel?>(null));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnPageWhenCheckExists()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checkViewModel = new CheckEditViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            ConfigurationJson = []
        };

        _mockCheckQuery.GetCheckForEditAsync(checkId).Returns(Task.FromResult<CheckEditViewModel?>(checkViewModel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateInputWithCheckData()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checkViewModel = new CheckEditViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "My Check",
            Description = "My Description",
            CheckType = CheckTypes.Tcp,
            Schedule = "120",
            TimeoutSeconds = 45,
            Enabled = false,
            ConfigurationJson = []
        };

        _mockCheckQuery.GetCheckForEditAsync(checkId).Returns(Task.FromResult<CheckEditViewModel?>(checkViewModel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(checkId);

        Assert.AreEqual(checkId, _pageModel.Input.Id);
        Assert.AreEqual(workspaceId, _pageModel.Input.WorkspaceId);
        Assert.AreEqual("My Check", _pageModel.Input.Name);
        Assert.AreEqual("My Description", _pageModel.Input.Description);
        Assert.AreEqual(CheckTypes.Tcp, _pageModel.Input.CheckType);
        Assert.AreEqual("120", _pageModel.Input.Schedule);
        Assert.AreEqual(45, _pageModel.Input.TimeoutSeconds);
        Assert.IsFalse(_pageModel.Input.Enabled);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldCallPopulateFromConfiguration()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var configJson = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["url"] = System.Text.Json.JsonSerializer.SerializeToElement("https://example.com")
        };
        var checkViewModel = new CheckEditViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            ConfigurationJson = configJson
        };

        _mockCheckQuery.GetCheckForEditAsync(checkId).Returns(Task.FromResult<CheckEditViewModel?>(checkViewModel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(checkId);

        _mockConfigService.Received(1).PopulateFromConfiguration(
            Arg.Is<EditModel.InputModel>(i => i.Id == checkId),
            Arg.Is<Dictionary<string, System.Text.Json.JsonElement>>(d => d.ContainsKey("url")));
    }

    [TestMethod]
    public async Task OnGetAsyncShouldPopulateCheckTypes()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        var checkViewModel = new CheckEditViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            ConfigurationJson = []
        };

        _mockCheckQuery.GetCheckForEditAsync(checkId).Returns(Task.FromResult<CheckEditViewModel?>(checkViewModel));

        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(checkId);

        Assert.IsNotEmpty(_pageModel.CheckTypes);
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Http));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Tcp));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Ping));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Dns));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Tls));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Script));
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var checkViewModel = new CheckEditViewModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true,
            ConfigurationJson = []
        };

        _mockCheckQuery.GetCheckForEditAsync(checkId).Returns(Task.FromResult<CheckEditViewModel?>(checkViewModel));
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetAsync(checkId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public void OnGetConfigFieldsShouldSetCheckTypeAndReturnPage()
    {
        var result = _pageModel.OnGetConfigFields(CheckTypes.Dns);

        Assert.AreEqual(CheckTypes.Dns, _pageModel.Input.CheckType);
        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallValidateConfiguration()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>()).Returns([]);
        _mockCheckCommand.UpdateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Update Test",
            CheckType = CheckTypes.Ping,
            Schedule = "90",
            TimeoutSeconds = 20,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        _mockConfigService.Received(1).ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Is<EditModel.InputModel>(i => i.Name == "Update Test"));
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallBuildConfigurationWhenModelStateIsValid()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>()).Returns([]);
        _mockCheckCommand.UpdateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Build Test",
            CheckType = CheckTypes.Script,
            Schedule = "300",
            TimeoutSeconds = 60,
            Enabled = false
        };

        await _pageModel.OnPostAsync();

        _mockConfigService.Received(1).BuildConfiguration(
            Arg.Is<EditModel.InputModel>(i => i.CheckType == CheckTypes.Script && i.Schedule == "300"));
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallUpdateCheckAsyncWithCorrectParameters()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var expectedConfig = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["host"] = System.Text.Json.JsonSerializer.SerializeToElement("example.com")
        };
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>()).Returns(expectedConfig);
        _mockCheckCommand.UpdateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Updated Check",
            Description = "Updated Description",
            CheckType = CheckTypes.Tls,
            Schedule = "3600",
            TimeoutSeconds = 15,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        await _mockCheckCommand.Received(1).UpdateCheckAsync(
            checkId,
            "Updated Check",
            "Updated Description",
            CheckTypes.Tls,
            "3600",
            15,
            expectedConfig,
            true,
            Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulUpdate()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>()).Returns([]);
        _mockCheckCommand.UpdateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("Index", redirect.PageName);
        Assert.AreEqual(workspaceId, redirect.RouteValues?["workspaceId"]);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageForEnabledCheck()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>()).Returns([]);
        _mockCheckCommand.UpdateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Enabled Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        Assert.Contains("Enabled Check", message);
        Assert.Contains("updated successfully", message);
        Assert.Contains("will be checked shortly", message);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldSetSuccessMessageForDisabledCheck()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>()).Returns([]);
        _mockCheckCommand.UpdateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Disabled Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = false
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        Assert.Contains("Disabled Check", message);
        Assert.Contains("updated successfully", message);
        Assert.DoesNotContain("will be checked shortly", message);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenModelStateIsInvalid()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Invalid Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenValidationFails()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _mockConfigService.When(x => x.ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Any<EditModel.InputModel>()))
            .Do(callInfo =>
            {
                var modelState = callInfo.ArgAt<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(0);
                modelState.AddModelError("Input.TcpPort", "Invalid port");
            });

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Invalid Check",
            CheckType = CheckTypes.Tcp,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallUpdateCheckAsyncWhenModelStateIsInvalid()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Invalid Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        await _pageModel.OnPostAsync();

        await _mockCheckCommand.DidNotReceive().UpdateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRepopulateCheckTypesWhenValidationFails()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _mockConfigService.When(x => x.ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Any<EditModel.InputModel>()))
            .Do(callInfo =>
            {
                var modelState = callInfo.ArgAt<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(0);
                modelState.AddModelError("Input.DnsHostname", "Hostname is required");
            });

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Dns,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        Assert.IsNotEmpty(_pageModel.CheckTypes);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnBadRequestWhenUpdateCheckAsyncReturnsFalse()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>()).Returns([]);
        _mockCheckCommand.UpdateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(false));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Non-existent Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<BadRequestResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldHandleNullDescription()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockConfigService.BuildConfiguration(Arg.Any<EditModel.InputModel>()).Returns([]);
        _mockCheckCommand.UpdateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(true));

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "No Description",
            Description = null,
            CheckType = CheckTypes.Ping,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        await _mockCheckCommand.Received(1).UpdateCheckAsync(
            checkId,
            "No Description",
            Arg.Is<string>(s => s == null),
            CheckTypes.Ping,
            "60",
            30,
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            true,
            Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExistDuringValidationFailure()
    {
        var checkId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        _mockConfigService.When(x => x.ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Any<EditModel.InputModel>()))
            .Do(callInfo =>
            {
                var modelState = callInfo.ArgAt<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(0);
                modelState.AddModelError("Input.Name", "Invalid name");
            });

        _pageModel.Input = new EditModel.InputModel
        {
            Id = checkId,
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }
}
