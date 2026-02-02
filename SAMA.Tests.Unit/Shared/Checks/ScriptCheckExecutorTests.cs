using System.Diagnostics;
using System.Text.Json;
using NSubstitute;
using SAMA.Shared.Checks;
using SAMA.Shared.Constants;
using SAMA.Shared.Wrappers;

namespace SAMA.Tests.Unit.Shared.Checks;

[TestClass]
public class ScriptCheckExecutorTests
{
    private ProcessWrapper _mockWrapper = null!;
    private ProcessFactory _mockFactory = null!;
    private ScriptCheckExecutor _executor = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockWrapper = Substitute.For<ProcessWrapper>();
        _mockFactory = Substitute.For<ProcessFactory>();
        _mockFactory.CreateProcess().Returns(_mockWrapper);
        _executor = new ScriptCheckExecutor(_mockFactory);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenScriptPathNotConfigured()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.AreEqual("Script path not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnUpForSuccessfulScriptExecution()
    {
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/path/to/script.sh"),
            [ConfigurationKeys.ScriptCheck.Arguments] = JsonSerializer.SerializeToElement("--arg value"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNotNull(result.ResponseTimeMs);
        Assert.IsGreaterThanOrEqualTo(0, result.ResponseTimeMs.Value);
        Assert.AreEqual(0, result.StatusCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownForNonZeroExitCode()
    {
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(1);
        _mockWrapper.ReadStandardErrorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult("Test error occurred"));

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/path/to/script.sh"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ResponseTimeMs);
        Assert.AreEqual(1, result.StatusCode);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Script exited with code 1 (expected 0)", result.ErrorMessage);
        Assert.Contains("Test error occurred", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnUpForMatchingNonZeroExitCode()
    {
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(42);

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/path/to/script.sh"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(42),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNotNull(result.ResponseTimeMs);
        Assert.AreEqual(42, result.StatusCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownForProcessStartException()
    {
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>()))
            .Do(_ => throw new System.ComponentModel.Win32Exception("File not found"));

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/nonexistent/script.sh"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("Script execution failed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldHandleScriptWithoutArguments()
    {
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/path/to/script.sh"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNotNull(result.ResponseTimeMs);
        Assert.AreEqual(0, result.StatusCode);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldUseDefaultExpectedExitCode()
    {
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/path/to/script.sh"),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.AreEqual(0, result.StatusCode);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldHandleTimeout()
    {
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>())).Do(_ => { });
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.Delay(10000, callInfo.Arg<CancellationToken>()));

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/path/to/script.sh"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(1)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ResponseTimeMs);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldHandleCancellation()
    {
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>())).Do(_ => { });
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.Delay(10000, callInfo.Arg<CancellationToken>()));

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/path/to/script.sh"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(30)
        };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        var result = await _executor.ExecuteAsync(config, cts.Token);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldMeasureExecutionTime()
    {
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>())).Do(_ => { });
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(100));
        _mockWrapper.ExitCode.Returns(0);

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/path/to/script.sh"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);
        Assert.IsNotNull(result.ResponseTimeMs);
        Assert.IsGreaterThan(0, result.ResponseTimeMs.Value);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldNotIncludeStderrWhenEmpty()
    {
        _mockWrapper.When(x => x.Start(Arg.Any<ProcessStartInfo>())).Do(_ => { });
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(1);
        _mockWrapper.ReadStandardErrorAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(string.Empty));

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("/path/to/script.sh"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.AreEqual("Script exited with code 1 (expected 0)", result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenInlineScriptMissingPlaceholder()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("bash"),
            [ConfigurationKeys.ScriptCheck.Arguments] = JsonSerializer.SerializeToElement("--some-arg"),
            [ConfigurationKeys.ScriptCheck.Content] = JsonSerializer.SerializeToElement("echo 'test'"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains(CheckDefaults.ScriptFilePlaceholder, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReturnDownWhenInlineScriptMissingArguments()
    {
        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("bash"),
            [ConfigurationKeys.ScriptCheck.Content] = JsonSerializer.SerializeToElement("echo 'test'"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Down, result.Status);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains(CheckDefaults.ScriptFilePlaceholder, result.ErrorMessage);
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldReplacePlaceholderWithTempFilePath()
    {
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("bash"),
            [ConfigurationKeys.ScriptCheck.Arguments] = JsonSerializer.SerializeToElement(CheckDefaults.ScriptFilePlaceholder),
            [ConfigurationKeys.ScriptCheck.Content] = JsonSerializer.SerializeToElement("echo 'test'"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        var result = await _executor.ExecuteAsync(config);

        Assert.AreEqual(CheckStatuses.Up, result.Status);

        _mockWrapper.Received(1).Start(Arg.Is<ProcessStartInfo>(psi =>
            psi.FileName == "bash" &&
            !psi.Arguments.Contains(CheckDefaults.ScriptFilePlaceholder) &&
            psi.Arguments.Contains("sama-script-") &&
            psi.Arguments.EndsWith(".ps1", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ExecuteAsyncShouldCleanupTempFileAfterExecution()
    {
        _mockWrapper.WaitForExitAsync(Arg.Any<CancellationToken>()).Returns(Task.Delay(10));
        _mockWrapper.ExitCode.Returns(0);

        string? capturedTempFilePath = null;
        _mockWrapper.When(w => w.Start(Arg.Any<ProcessStartInfo>()))
            .Do(callInfo =>
            {
                var psi = callInfo.Arg<ProcessStartInfo>();
                capturedTempFilePath = psi.Arguments;
            });

        var config = new Dictionary<string, JsonElement>
        {
            [ConfigurationKeys.ScriptCheck.Path] = JsonSerializer.SerializeToElement("bash"),
            [ConfigurationKeys.ScriptCheck.Arguments] = JsonSerializer.SerializeToElement(CheckDefaults.ScriptFilePlaceholder),
            [ConfigurationKeys.ScriptCheck.Content] = JsonSerializer.SerializeToElement("echo 'test'"),
            [ConfigurationKeys.ScriptCheck.ExpectedExitCode] = JsonSerializer.SerializeToElement(0),
            [ConfigurationKeys.Common.TimeoutSeconds] = JsonSerializer.SerializeToElement(10)
        };

        await _executor.ExecuteAsync(config);

        Assert.IsNotNull(capturedTempFilePath);
        Assert.IsFalse(File.Exists(capturedTempFilePath), "Temp file should be deleted after execution");
    }
}
