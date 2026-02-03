using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Web.Services;

namespace SAMA.Tests.Unit.Web.Services;

[TestClass]
public class ScriptOutputBufferTests
{
    private ILogger<ScriptOutputBuffer> _mockLogger = null!;
    private ScriptOutputBuffer _buffer = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = Substitute.For<ILogger<ScriptOutputBuffer>>();
        _buffer = new ScriptOutputBuffer(_mockLogger);
    }

    [TestMethod]
    public void AddShouldStoreOutputForCheck()
    {
        var checkId = Guid.NewGuid();
        var resultId = Guid.NewGuid();

        _buffer.Add(checkId, resultId, "stdout content", "stderr content");

        var outputs = _buffer.GetOutputs(checkId).ToList();
        Assert.HasCount(1, outputs);
        Assert.AreEqual(resultId, outputs[0].CheckResultId);
        Assert.AreEqual("stdout content", outputs[0].StandardOutput);
        Assert.AreEqual("stderr content", outputs[0].StandardError);
    }

    [TestMethod]
    public void AddShouldNotStoreWhenBothOutputsAreEmpty()
    {
        var checkId = Guid.NewGuid();
        var resultId = Guid.NewGuid();

        _buffer.Add(checkId, resultId, "", null);

        var outputs = _buffer.GetOutputs(checkId).ToList();
        Assert.IsEmpty(outputs);
    }

    [TestMethod]
    public void AddShouldNotStoreWhenBothOutputsAreWhitespace()
    {
        var checkId = Guid.NewGuid();
        var resultId = Guid.NewGuid();

        _buffer.Add(checkId, resultId, "   ", "  \n  ");

        var outputs = _buffer.GetOutputs(checkId).ToList();
        Assert.IsEmpty(outputs);
    }

    [TestMethod]
    public void GetOutputsShouldReturnMostRecentFirst()
    {
        var checkId = Guid.NewGuid();
        var resultId1 = Guid.NewGuid();
        var resultId2 = Guid.NewGuid();

        _buffer.Add(checkId, resultId1, "first", null);
        _buffer.Add(checkId, resultId2, "second", null);

        var outputs = _buffer.GetOutputs(checkId).ToList();
        Assert.HasCount(2, outputs);
        Assert.AreEqual(resultId2, outputs[0].CheckResultId);
        Assert.AreEqual(resultId1, outputs[1].CheckResultId);
    }

    [TestMethod]
    public void GetOutputsShouldReturnEmptyForUnknownCheck()
    {
        var unknownCheckId = Guid.NewGuid();

        var outputs = _buffer.GetOutputs(unknownCheckId).ToList();

        Assert.IsEmpty(outputs);
    }

    [TestMethod]
    public void GetLatestOutputShouldReturnMostRecentEntry()
    {
        var checkId = Guid.NewGuid();
        var resultId1 = Guid.NewGuid();
        var resultId2 = Guid.NewGuid();

        _buffer.Add(checkId, resultId1, "first", null);
        _buffer.Add(checkId, resultId2, "second", null);

        var latest = _buffer.GetLatestOutput(checkId);

        Assert.IsNotNull(latest);
        Assert.AreEqual(resultId2, latest.CheckResultId);
        Assert.AreEqual("second", latest.StandardOutput);
    }

    [TestMethod]
    public void GetLatestOutputShouldReturnNullForUnknownCheck()
    {
        var unknownCheckId = Guid.NewGuid();

        var latest = _buffer.GetLatestOutput(unknownCheckId);

        Assert.IsNull(latest);
    }

    [TestMethod]
    public void AddShouldLogStdoutToSerilog()
    {
        var checkId = Guid.NewGuid();
        var resultId = Guid.NewGuid();

        _buffer.Add(checkId, resultId, "test output", null);

        _mockLogger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [TestMethod]
    public void AddShouldLogStderrToSerilogAsWarning()
    {
        var checkId = Guid.NewGuid();
        var resultId = Guid.NewGuid();

        _buffer.Add(checkId, resultId, null, "error output");

        _mockLogger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [TestMethod]
    public void AddShouldEnforceMaxEntriesPerCheck()
    {
        var checkId = Guid.NewGuid();

        for (int i = 0; i < 25; i++)
        {
            _buffer.Add(checkId, Guid.NewGuid(), $"output {i}", null);
        }

        var outputs = _buffer.GetOutputs(checkId).ToList();
        Assert.HasCount(20, outputs);
        Assert.AreEqual("output 24", outputs[0].StandardOutput);
        Assert.AreEqual("output 5", outputs[19].StandardOutput);
    }

    [TestMethod]
    public void AddShouldKeepOutputsSeparatedByCheckId()
    {
        var checkId1 = Guid.NewGuid();
        var checkId2 = Guid.NewGuid();

        _buffer.Add(checkId1, Guid.NewGuid(), "check1 output", null);
        _buffer.Add(checkId2, Guid.NewGuid(), "check2 output", null);

        var outputs1 = _buffer.GetOutputs(checkId1).ToList();
        var outputs2 = _buffer.GetOutputs(checkId2).ToList();

        Assert.HasCount(1, outputs1);
        Assert.AreEqual("check1 output", outputs1[0].StandardOutput);
        Assert.HasCount(1, outputs2);
        Assert.AreEqual("check2 output", outputs2[0].StandardOutput);
    }

    [TestMethod]
    public void AddShouldTruncateLargeStdout()
    {
        var checkId = Guid.NewGuid();
        var resultId = Guid.NewGuid();
        var largeOutput = new string('x', 100_000); // 100KB

        _buffer.Add(checkId, resultId, largeOutput, null);

        var outputs = _buffer.GetOutputs(checkId).ToList();
        Assert.HasCount(1, outputs);
        var stdout = outputs[0].StandardOutput!;
        Assert.IsLessThan(60_000, stdout.Length, "Output should be truncated to around 50KB");
        Assert.Contains("[...truncated", stdout);
        Assert.IsTrue(stdout.EndsWith("xxxx", StringComparison.Ordinal), "Should keep end of output");
    }

    [TestMethod]
    public void AddShouldTruncateLargeStderr()
    {
        var checkId = Guid.NewGuid();
        var resultId = Guid.NewGuid();
        var largeError = new string('e', 100_000); // 100KB

        _buffer.Add(checkId, resultId, null, largeError);

        var outputs = _buffer.GetOutputs(checkId).ToList();
        Assert.HasCount(1, outputs);
        var stderr = outputs[0].StandardError!;
        Assert.IsLessThan(60_000, stderr.Length, "Error should be truncated to around 50KB");
        Assert.Contains("[...truncated", stderr);
    }

    [TestMethod]
    public void AddShouldNotTruncateSmallOutput()
    {
        var checkId = Guid.NewGuid();
        var resultId = Guid.NewGuid();
        var smallOutput = new string('x', 1000); // 1KB

        _buffer.Add(checkId, resultId, smallOutput, null);

        var outputs = _buffer.GetOutputs(checkId).ToList();
        Assert.HasCount(1, outputs);
        var stdout = outputs[0].StandardOutput!;
        Assert.AreEqual(1000, stdout.Length);
        Assert.IsFalse(stdout.Contains("[...truncated", StringComparison.Ordinal));
    }
}
