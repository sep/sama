using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Shared.Wrappers;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Services;
using SAMA.Web.Services.NotificationChannels;

namespace SAMA.Tests.Unit.Web.Services.NotificationChannels;

[TestClass]
public class ScriptChannelHandlerTests
{
    private ProcessWrapper _mockWrapper = null!;
    private ProcessFactory _mockFactory = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private ILogger<ScriptChannelHandler> _logger = null!;
    private ScriptChannelHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWrapper = Substitute.For<ProcessWrapper>();
        _mockFactory = Substitute.For<ProcessFactory>();
        _mockFactory.CreateProcess().Returns(_mockWrapper);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null, null, null, null);
        _mockGlobalSettings.NotificationTimeoutSeconds.Returns(30);
        _logger = Substitute.For<ILogger<ScriptChannelHandler>>();
        _handler = new ScriptChannelHandler(_mockFactory, _mockGlobalSettings, _logger);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldFailWhenScriptPathNotConfigured()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Script",
            ChannelType = ChannelTypes.Script,
            ConfigurationJson = new Dictionary<string, JsonElement>(),
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = CreateStatusAlertContext();

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Script path not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldFailWhenScriptPathIsEmpty()
    {
        var channel = CreateChannel("/path/to/script.sh");
        channel.ConfigurationJson[ConfigurationKeys.Script.Path] = JsonSerializer.SerializeToElement("");

        var context = CreateStatusAlertContext();

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Script path not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnSuccessWhenScriptExitsWithZero()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext();

        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenScriptExitsWithNonZero()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext();

        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(1);
        _mockWrapper.ReadStandardErrorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult("Script error occurred"));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Script exited with code 1", result.ErrorMessage);
        Assert.Contains("Script error occurred", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldPassEnvironmentVariables()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext(
            checkName: "HTTP Check",
            status: CheckStatuses.Down,
            errorMessage: "Connection refused",
            responseTimeMs: 150,
            workspaceName: "Production",
            isRecovery: false,
            consecutiveFailures: 3
        );

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.AreEqual("HTTP Check", capturedStartInfo.Environment["SAMA_CHECK_NAME"]);
        Assert.AreEqual(context.CheckId.ToString(), capturedStartInfo.Environment["SAMA_CHECK_ID"]);
        Assert.AreEqual(CheckStatuses.Down, capturedStartInfo.Environment["SAMA_STATUS"]);
        Assert.AreEqual("Connection refused", capturedStartInfo.Environment["SAMA_ERROR_MESSAGE"]);
        Assert.AreEqual("150", capturedStartInfo.Environment["SAMA_RESPONSE_TIME_MS"]);
        Assert.AreEqual("Production", capturedStartInfo.Environment["SAMA_WORKSPACE"]);
        Assert.AreEqual("False", capturedStartInfo.Environment["SAMA_IS_RECOVERY"]);
        Assert.AreEqual("3", capturedStartInfo.Environment["SAMA_CONSECUTIVE_FAILURES"]);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldNotSetErrorMessageEnvWhenNull()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext(errorMessage: null);

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.IsFalse(capturedStartInfo.Environment.ContainsKey("SAMA_ERROR_MESSAGE"));
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldNotSetResponseTimeEnvWhenNull()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext(responseTimeMs: null);

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.IsFalse(capturedStartInfo.Environment.ContainsKey("SAMA_RESPONSE_TIME_MS"));
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldPassArgumentsToScript()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh", "--level critical --json");
        var context = CreateStatusAlertContext();

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.AreEqual("--level critical --json", capturedStartInfo.Arguments);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleScriptWithoutArguments()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh", arguments: null);
        var context = CreateStatusAlertContext();

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.IsTrue(string.IsNullOrEmpty(capturedStartInfo.Arguments));
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleTimeout()
    {
        _mockGlobalSettings.NotificationTimeoutSeconds.Returns(1);

        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext();

        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>())).Do(_ => { });
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.Delay(60000, callInfo.ArgAt<CancellationToken>(0)));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext();

        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>())).Do(_ => { });
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.Delay(60000, callInfo.ArgAt<CancellationToken>(0)));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var result = await _handler.SendStatusAlertAsync(channel, context, cts.Token);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Request cancelled", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleWin32Exception()
    {
        var channel = CreateChannel("/nonexistent/script.sh");
        var context = CreateStatusAlertContext();

        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(_ => throw new System.ComponentModel.Win32Exception("File not found"));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Failed to start script", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleUnexpectedException()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext();

        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(_ => throw new InvalidOperationException("Unexpected error"));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Unexpected error", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldNotIncludeStderrWhenEmpty()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext();

        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(1);
        _mockWrapper.ReadStandardErrorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(string.Empty));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Script exited with code 1", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldTruncateLongStderr()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusAlertContext();

        var longError = new string('x', 1000);
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(1);
        _mockWrapper.ReadStandardErrorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(longError));

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.IsLessThan(result.ErrorMessage.Length, 520);
        Assert.Contains("...", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldFailWhenScriptPathNotConfigured()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Script",
            ChannelType = ChannelTypes.Script,
            ConfigurationJson = new Dictionary<string, JsonElement>(),
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = CreateLifecycleEventContext();

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Script path not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldReturnSuccessWhenScriptExitsWithZero()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateLifecycleEventContext();

        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldPassEnvironmentVariables()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateLifecycleEventContext(
            eventType: EventTypes.CheckCreated,
            checkName: "New HTTP Check",
            checkType: CheckTypes.Http,
            workspaceName: "Staging",
            performedBy: "admin@example.com",
            configurationChanges: new Dictionary<string, object> { ["Url"] = "https://example.com", ["Method"] = "GET" }
        );

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.AreEqual(EventTypes.CheckCreated, capturedStartInfo.Environment["SAMA_EVENT_TYPE"]);
        Assert.AreEqual("New HTTP Check", capturedStartInfo.Environment["SAMA_CHECK_NAME"]);
        Assert.AreEqual(context.CheckId.ToString(), capturedStartInfo.Environment["SAMA_CHECK_ID"]);
        Assert.AreEqual(CheckTypes.GetShortDisplayName(CheckTypes.Http), capturedStartInfo.Environment["SAMA_CHECK_TYPE"]);
        Assert.AreEqual("Staging", capturedStartInfo.Environment["SAMA_WORKSPACE"]);
        Assert.AreEqual("admin@example.com", capturedStartInfo.Environment["SAMA_PERFORMED_BY"]);
        Assert.AreEqual("Url,Method", capturedStartInfo.Environment["SAMA_CHANGED_FIELDS"]);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldNotSetChangedFieldsWhenNull()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateLifecycleEventContext(configurationChanges: null);

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.IsFalse(capturedStartInfo.Environment.ContainsKey("SAMA_CHANGED_FIELDS"));
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldNotSetChangedFieldsWhenEmpty()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateLifecycleEventContext(configurationChanges: new Dictionary<string, object>());

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.IsFalse(capturedStartInfo.Environment.ContainsKey("SAMA_CHANGED_FIELDS"));
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldReturnFailureWhenScriptExitsWithNonZero()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateLifecycleEventContext();

        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(2);
        _mockWrapper.ReadStandardErrorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult("Lifecycle script error"));

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Script exited with code 2", result.ErrorMessage);
        Assert.Contains("Lifecycle script error", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldFailWhenScriptPathNotConfigured()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            Name = "Test Script",
            ChannelType = ChannelTypes.Script,
            ConfigurationJson = new Dictionary<string, JsonElement>(),
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var context = CreateStatusChangeEventContext();

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Script path not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldReturnSuccessWhenScriptExitsWithZero()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusChangeEventContext();

        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldPassEnvironmentVariables()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusChangeEventContext(
            checkName: "HTTP Check",
            workspaceName: "Production",
            previousStatus: CheckStatuses.Up,
            newStatus: CheckStatuses.Down,
            responseTimeMs: 3000,
            errorMessage: "Connection refused");

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.AreEqual("HTTP Check", capturedStartInfo.Environment["SAMA_CHECK_NAME"]);
        Assert.AreEqual(context.CheckId.ToString(), capturedStartInfo.Environment["SAMA_CHECK_ID"]);
        Assert.AreEqual(CheckStatuses.Up, capturedStartInfo.Environment["SAMA_PREVIOUS_STATUS"]);
        Assert.AreEqual(CheckStatuses.Down, capturedStartInfo.Environment["SAMA_NEW_STATUS"]);
        Assert.AreEqual("3000", capturedStartInfo.Environment["SAMA_RESPONSE_TIME_MS"]);
        Assert.AreEqual("Connection refused", capturedStartInfo.Environment["SAMA_ERROR_MESSAGE"]);
        Assert.AreEqual("Production", capturedStartInfo.Environment["SAMA_WORKSPACE"]);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldNotSetErrorMessageEnvWhenNull()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusChangeEventContext(errorMessage: null);

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.IsFalse(capturedStartInfo.Environment.ContainsKey("SAMA_ERROR_MESSAGE"));
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldNotSetResponseTimeEnvWhenNull()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusChangeEventContext(responseTimeMs: null);

        ProcessStartInfo? capturedStartInfo = null;
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo => capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0));
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNotNull(capturedStartInfo);
        Assert.IsFalse(capturedStartInfo.Environment.ContainsKey("SAMA_RESPONSE_TIME_MS"));
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldReturnFailureWhenScriptExitsWithNonZero()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusChangeEventContext();

        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(3);
        _mockWrapper.ReadStandardErrorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult("Status change script error"));

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Script exited with code 3", result.ErrorMessage);
        Assert.Contains("Status change script error", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("/usr/local/bin/notify.sh");
        var context = CreateStatusChangeEventContext();

        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>())).Do(_ => { });
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.Delay(60000, callInfo.ArgAt<CancellationToken>(0)));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var result = await _handler.SendStatusChangeEventAsync(channel, context, cts.Token);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Request cancelled", result.ErrorMessage);
    }

    private static NotificationChannel CreateChannel(string scriptPath, string? arguments = null)
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.Script.Path] = JsonSerializer.SerializeToElement(scriptPath)
        };

        if (arguments != null)
        {
            config[ConfigurationKeys.Script.Arguments] = JsonSerializer.SerializeToElement(arguments);
        }

        return new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Script Channel",
            ChannelType = ChannelTypes.Script,
            ConfigurationJson = config,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static StatusAlertContext CreateStatusAlertContext(
        string checkName = "Test Check",
        string status = CheckStatuses.Down,
        string? errorMessage = "Test error",
        int? responseTimeMs = 100,
        string workspaceName = "Test Workspace",
        bool isRecovery = false,
        int consecutiveFailures = 1)
    {
        return new StatusAlertContext
        {
            CheckName = checkName,
            CheckId = Guid.NewGuid(),
            Status = status,
            ErrorMessage = errorMessage,
            ResponseTimeMs = responseTimeMs,
            Timestamp = DateTimeOffset.UtcNow,
            WorkspaceName = workspaceName,
            IsRecovery = isRecovery,
            ConsecutiveFailures = consecutiveFailures
        };
    }

    private static LifecycleEventContext CreateLifecycleEventContext(
        string eventType = EventTypes.CheckCreated,
        string checkName = "Test Check",
        string checkType = CheckTypes.Http,
        string workspaceName = "Test Workspace",
        string performedBy = "test@example.com",
        Dictionary<string, object>? configurationChanges = null)
    {
        return new LifecycleEventContext
        {
            EventType = eventType,
            CheckId = Guid.NewGuid(),
            CheckName = checkName,
            CheckType = checkType,
            WorkspaceName = workspaceName,
            Timestamp = DateTimeOffset.UtcNow,
            PerformedBy = performedBy,
            ConfigurationChanges = configurationChanges
        };
    }

    private static StatusChangeEventContext CreateStatusChangeEventContext(
        string checkName = "Test Check",
        string workspaceName = "Test Workspace",
        string previousStatus = CheckStatuses.Up,
        string newStatus = CheckStatuses.Down,
        int? responseTimeMs = null,
        string? errorMessage = null)
    {
        return new StatusChangeEventContext
        {
            CheckId = Guid.NewGuid(),
            CheckName = checkName,
            WorkspaceName = workspaceName,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ResponseTimeMs = responseTimeMs,
            ErrorMessage = errorMessage,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
