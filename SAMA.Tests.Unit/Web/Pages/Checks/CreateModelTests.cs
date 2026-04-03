using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Pages.Checks;
using SAMA.Web.Services;
using SAMA.Web.Services.Commands;
using SAMA.Web.Services.Queries;

namespace SAMA.Tests.Unit.Web.Pages.Checks;

[TestClass]
public class CreateModelTests
{
    private WorkspaceQueryService _mockWorkspaceQuery = null!;
    private CheckConfigurationService _mockCheckConfigService = null!;
    private CheckCommandService _mockCheckCommand = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private CreateModel _pageModel = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWorkspaceQuery = Substitute.For<WorkspaceQueryService>(null!, null!);
        _mockCheckConfigService = Substitute.For<CheckConfigurationService>();
        _mockCheckCommand = Substitute.For<CheckCommandService>(null!, null!, null!, null!, null!);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null!, null!, null!, null!);

        _pageModel = new CreateModel(_mockWorkspaceQuery, _mockCheckConfigService, _mockCheckCommand, _mockGlobalSettings);
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
    public async Task OnGetAsyncShouldPopulateCheckTypes()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.IsNotEmpty(_pageModel.CheckTypes);
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Http));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Tcp));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Ping));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Dns));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Tls));
        Assert.IsTrue(_pageModel.CheckTypes.Any(ct => ct.Value == CheckTypes.Script));
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetInputWorkspaceId()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(workspaceId, _pageModel.Input.WorkspaceId);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldSetTimeoutFromGlobalSettings()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockGlobalSettings.DefaultCheckTimeoutSeconds.Returns(45);

        await _pageModel.OnGetAsync(workspaceId);

        Assert.AreEqual(45, _pageModel.Input.TimeoutSeconds);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExist()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        var result = await _pageModel.OnGetAsync(workspaceId);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    public async Task OnGetAsyncShouldReturnRedirectWhenWorkspaceIdIsNull()
    {
        var result = await _pageModel.OnGetAsync(null);

        Assert.IsInstanceOfType<RedirectToPageResult>(result);
        var redirect = (RedirectToPageResult)result;
        Assert.AreEqual("/Workspaces/Index", redirect.PageName);
    }

    [TestMethod]
    public void OnGetConfigFieldsShouldSetCheckTypeAndReturnPage()
    {
        var result = _pageModel.OnGetConfigFields(CheckTypes.Http);

        Assert.AreEqual(CheckTypes.Http, _pageModel.Input.CheckType);
        Assert.IsInstanceOfType<PageResult>(result);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallValidateConfiguration()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckConfigService.BuildConfiguration(Arg.Any<CreateModel.InputModel>()).Returns([]);
        _mockCheckCommand.CreateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(Guid.NewGuid()));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        _mockCheckConfigService.Received(1).ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Is<CreateModel.InputModel>(i => i.Name == "Test Check"));
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallBuildConfigurationWhenModelStateIsValid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckConfigService.BuildConfiguration(Arg.Any<CreateModel.InputModel>()).Returns([]);
        _mockCheckCommand.CreateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(Guid.NewGuid()));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Tcp,
            Schedule = "120",
            TimeoutSeconds = 15,
            Enabled = false
        };

        await _pageModel.OnPostAsync();

        _mockCheckConfigService.Received(1).BuildConfiguration(
            Arg.Is<CreateModel.InputModel>(i => i.CheckType == CheckTypes.Tcp && i.Schedule == "120"));
    }

    [TestMethod]
    public async Task OnPostAsyncShouldCallCreateCheckAsyncWithCorrectParameters()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        var expectedConfig = new Dictionary<string, System.Text.Json.JsonElement>();
        _mockCheckConfigService.BuildConfiguration(Arg.Any<CreateModel.InputModel>()).Returns(expectedConfig);
        _mockCheckCommand.CreateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(Guid.NewGuid()));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "My Check",
            Description = "My Description",
            CheckType = CheckTypes.Dns,
            Schedule = "300",
            TimeoutSeconds = 20,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        await _mockCheckCommand.Received(1).CreateCheckAsync(
            workspaceId,
            "My Check",
            "My Description",
            CheckTypes.Dns,
            "300",
            20,
            expectedConfig,
            true,
            Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldRedirectToIndexAfterSuccessfulCreation()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckConfigService.BuildConfiguration(Arg.Any<CreateModel.InputModel>()).Returns([]);
        _mockCheckCommand.CreateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(Guid.NewGuid()));

        _pageModel.Input = new CreateModel.InputModel
        {
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
    public async Task OnPostAsyncShouldSetSuccessMessageInTempData()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckConfigService.BuildConfiguration(Arg.Any<CreateModel.InputModel>()).Returns([]);
        _mockCheckCommand.CreateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(Guid.NewGuid()));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Success Check",
            CheckType = CheckTypes.Ping,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        Assert.IsTrue(_pageModel.TempData.ContainsKey("SuccessMessage"));
        var message = _pageModel.TempData["SuccessMessage"]?.ToString();
        Assert.IsNotNull(message);
        StringAssert.Contains(message, "Success Check");
        StringAssert.Contains(message, "created successfully");
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnPageWhenModelStateIsInvalid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new CreateModel.InputModel
        {
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
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _mockCheckConfigService.When(x => x.ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Any<CreateModel.InputModel>()))
            .Do(callInfo =>
            {
                var modelState = callInfo.ArgAt<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(0);
                modelState.AddModelError("Input.HttpUrl", "Invalid URL");
            });

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Invalid Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        var result = await _pageModel.OnPostAsync();

        Assert.IsInstanceOfType<PageResult>(result);
        Assert.IsFalse(_pageModel.ModelState.IsValid);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldNotCallCreateCheckAsyncWhenModelStateIsInvalid()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Invalid Check",
            CheckType = CheckTypes.Http,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        _pageModel.ModelState.AddModelError("Input.Name", "Name is required");

        await _pageModel.OnPostAsync();

        await _mockCheckCommand.DidNotReceive().CreateCheckAsync(
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
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));

        _mockCheckConfigService.When(x => x.ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Any<CreateModel.InputModel>()))
            .Do(callInfo =>
            {
                var modelState = callInfo.ArgAt<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(0);
                modelState.AddModelError("Input.TcpHost", "Host is required");
            });

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Test Check",
            CheckType = CheckTypes.Tcp,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        Assert.IsNotEmpty(_pageModel.CheckTypes);
    }

    [TestMethod]
    public async Task OnPostAsyncShouldHandleNullDescription()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new Workspace { Id = workspaceId, Name = "Test Workspace" };
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(workspace));
        _mockCheckConfigService.BuildConfiguration(Arg.Any<CreateModel.InputModel>()).Returns([]);
        _mockCheckCommand.CreateCheckAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            Arg.Any<bool>(),
            Arg.Any<string>()).Returns(Task.FromResult(Guid.NewGuid()));

        _pageModel.Input = new CreateModel.InputModel
        {
            WorkspaceId = workspaceId,
            Name = "Test Check",
            Description = null,
            CheckType = CheckTypes.Script,
            Schedule = "60",
            TimeoutSeconds = 30,
            Enabled = true
        };

        await _pageModel.OnPostAsync();

        await _mockCheckCommand.Received(1).CreateCheckAsync(
            workspaceId,
            "Test Check",
            Arg.Is<string?>(d => d == null),
            CheckTypes.Script,
            "60",
            30,
            Arg.Any<Dictionary<string, System.Text.Json.JsonElement>>(),
            true,
            Arg.Any<string>());
    }

    [TestMethod]
    public async Task OnPostAsyncShouldReturnNotFoundWhenWorkspaceDoesNotExistDuringValidationFailure()
    {
        var workspaceId = Guid.NewGuid();
        _mockWorkspaceQuery.GetWorkspaceByIdAsync(workspaceId).Returns(Task.FromResult<Workspace?>(null));

        _mockCheckConfigService.When(x => x.ValidateConfiguration(
            Arg.Any<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(),
            Arg.Any<CreateModel.InputModel>()))
            .Do(callInfo =>
            {
                var modelState = callInfo.ArgAt<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>(0);
                modelState.AddModelError("Input.Name", "Invalid name");
            });

        _pageModel.Input = new CreateModel.InputModel
        {
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
