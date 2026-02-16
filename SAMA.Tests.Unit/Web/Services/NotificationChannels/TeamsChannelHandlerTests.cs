using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SAMA.Data.Entities;
using SAMA.Shared.Constants;
using SAMA.Tests.Unit.TestUtilities;
using SAMA.Web.Constants;
using SAMA.Web.Models;
using SAMA.Web.Services;
using SAMA.Web.Services.NotificationChannels;

namespace SAMA.Tests.Unit.Web.Services.NotificationChannels;

[TestClass]
public class TeamsChannelHandlerTests
{
    private IHttpClientFactory _mockHttpClientFactory = null!;
    private HttpClient _httpClient = null!;
    private TestHttpMessageHandler _testHandler = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private ILogger<TeamsChannelHandler> _mockLogger = null!;
    private TeamsChannelHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        _testHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_testHandler);
        _mockHttpClientFactory.CreateClient(Arg.Any<string>()).Returns(_httpClient);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null, null, null, null);
        _mockGlobalSettings.NotificationTimeoutSeconds.Returns(30);
        _mockLogger = Substitute.For<ILogger<TeamsChannelHandler>>();
        _handler = new TeamsChannelHandler(_mockHttpClientFactory, _mockGlobalSettings, _mockLogger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient.Dispose();
        _testHandler.Dispose();
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnSuccessWhenTeamsApiReturnsOk()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext(status: CheckStatuses.Down, isRecovery: false);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("1")
        };

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(_testHandler.RequestReceived);
        Assert.AreEqual("https://outlook.office.com/webhook/TEST", _testHandler.RequestReceived.RequestUri?.ToString());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldIncludeCorrectPayloadStructure()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext(
            checkName: "API Health Check",
            status: CheckStatuses.Down,
            errorMessage: "Connection timeout",
            responseTimeMs: 5000,
            workspaceName: "Production",
            isRecovery: false,
            consecutiveFailures: 3);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsNotNull(_testHandler.RequestContent);
        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent);

        Assert.IsTrue(payload.TryGetProperty("type", out var typeElement));
        Assert.AreEqual("message", typeElement.GetString());

        Assert.IsTrue(payload.TryGetProperty("attachments", out var attachments));
        Assert.AreEqual(1, attachments.GetArrayLength());

        var attachment = attachments[0];
        Assert.AreEqual("application/vnd.microsoft.card.adaptive", attachment.GetProperty("contentType").GetString());

        var content = attachment.GetProperty("content");
        Assert.AreEqual("AdaptiveCard", content.GetProperty("type").GetString());
        Assert.AreEqual("1.4", content.GetProperty("version").GetString());

        Assert.IsTrue(content.TryGetProperty("body", out var body));
        Assert.IsGreaterThan(0, body.GetArrayLength());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldIncludeStatusInTitle()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext(checkName: "Test Check", status: CheckStatuses.Down, isRecovery: false);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body");
        var titleBlock = body[0];
        var titleText = titleBlock.GetProperty("text").GetString();

        Assert.Contains("down", titleText!);
        Assert.Contains("Test Check", titleText!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldIndicateRecoveryInTitle()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext(checkName: "Test Check", status: CheckStatuses.Up, isRecovery: true);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body");
        var titleBlock = body[0];
        var titleText = titleBlock.GetProperty("text").GetString();

        Assert.Contains("back up", titleText!);
        Assert.Contains("Test Check", titleText!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldIncludeAllRelevantFacts()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext(
            status: CheckStatuses.Down,
            errorMessage: "Connection refused",
            responseTimeMs: 1500,
            workspaceName: "Staging",
            consecutiveFailures: 2);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body").EnumerateArray().ToList();

        var factSetBlock = body.FirstOrDefault(b => b.GetProperty("type").GetString() == "FactSet");
        Assert.AreNotEqual(JsonValueKind.Undefined, factSetBlock.ValueKind);

        var facts = factSetBlock.GetProperty("facts").EnumerateArray().ToList();
        var factTitles = facts.Select(f => f.GetProperty("title").GetString()).ToList();

        Assert.Contains("Response Time", factTitles);
        Assert.Contains("Status", factTitles);
        Assert.Contains("Workspace", factTitles);

        var responseTimeFact = facts.First(f => f.GetProperty("title").GetString() == "Response Time");
        Assert.AreEqual("1500ms", responseTimeFact.GetProperty("value").GetString());

        var statusFact = facts.First(f => f.GetProperty("title").GetString() == "Status");
        Assert.AreEqual("Down", statusFact.GetProperty("value").GetString());

        var subtitleBlock = body[1];
        var subtitleText = subtitleBlock.GetProperty("text").GetString();
        Assert.Contains("2 consecutive", subtitleText!);

        var errorLabelBlock = body.FirstOrDefault(b =>
            b.TryGetProperty("text", out var text) &&
            text.GetString()!.Contains("**Error:**"));
        Assert.AreNotEqual(JsonValueKind.Undefined, errorLabelBlock.ValueKind);

        var errorMessageBlocks = body.Where(b =>
            b.TryGetProperty("fontType", out var fontType) &&
            fontType.GetString() == "monospace").ToList();
        Assert.AreNotEqual(0, errorMessageBlocks.Count);

        var errorMessageText = errorMessageBlocks[0].GetProperty("text").GetString();
        Assert.Contains("Connection refused", errorMessageText!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenWebhookUrlMissing()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Channel",
            ChannelType = "Teams",
            ConfigurationJson = [],
        };
        var context = CreateContext();

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Webhook URL not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenWebhookUrlEmpty()
    {
        var channel = CreateChannel("");
        var context = CreateContext();

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Webhook URL not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenTeamsApiReturnsError()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext();

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("invalid_payload")
        };

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("BadRequest", result.ErrorMessage!);
        Assert.Contains("invalid_payload", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleHttpRequestException()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext();

        _testHandler.ExceptionToThrow = new HttpRequestException("Network error");

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("HTTP error", result.ErrorMessage!);
        Assert.Contains("Network error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _handler.SendStatusAlertAsync(channel, context, cts.Token);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Request cancelled", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleTimeout()
    {
        _mockGlobalSettings.NotificationTimeoutSeconds.Returns(1);

        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext();

        _testHandler.SimulatedDelay = TimeSpan.FromSeconds(60);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleUnexpectedException()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext();

        _testHandler.ExceptionToThrow = new InvalidOperationException("Unexpected error");

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("Unexpected error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldSetSentAtTimestamp()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext();

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        var before = DateTimeOffset.UtcNow;
        var result = await _handler.SendStatusAlertAsync(channel, context);
        var after = DateTimeOffset.UtcNow;

        Assert.IsTrue(result.SentAt >= before && result.SentAt <= after);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldDisplayTwentyPlusWhenConsecutiveFailuresIs20()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext(
            status: CheckStatuses.Down,
            consecutiveFailures: 20);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body");
        var subtitleBlock = body[1];
        var subtitleText = subtitleBlock.GetProperty("text").GetString();

        Assert.Contains("20+", subtitleText!);
        Assert.DoesNotContain("**20 consecutive", subtitleText!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldDisplayExactCountWhenConsecutiveFailuresIsBelow20()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext(
            status: CheckStatuses.Down,
            consecutiveFailures: 15);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body");
        var subtitleBlock = body[1];
        var subtitleText = subtitleBlock.GetProperty("text").GetString();

        Assert.Contains("15 consecutive", subtitleText!);
        Assert.DoesNotContain("20+", subtitleText!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldDisplayTwentyPlusForRecoveryWhenConsecutiveFailuresWas20()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateContext(
            status: CheckStatuses.Up,
            isRecovery: true,
            consecutiveFailures: 20);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body");
        var subtitleBlock = body[1];
        var subtitleText = subtitleBlock.GetProperty("text").GetString();

        Assert.Contains("20+", subtitleText!);
        Assert.DoesNotContain("20 consecutive", subtitleText!);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldReturnSuccessWhenTeamsApiReturnsOk()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateLifecycleContext(eventType: EventTypes.CheckCreated);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("1")
        };

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(_testHandler.RequestReceived);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldIncludeCorrectPayloadForCheckCreated()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateLifecycleContext(
            eventType: EventTypes.CheckCreated,
            checkName: "API Health Check",
            checkType: "Http",
            workspaceName: "Production",
            performedBy: "admin@example.com");

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsNotNull(_testHandler.RequestContent);
        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent);

        Assert.IsTrue(payload.TryGetProperty("attachments", out var attachments));
        Assert.AreEqual(1, attachments.GetArrayLength());

        var content = attachments[0].GetProperty("content");
        var body = content.GetProperty("body");

        var titleBlock = body[0];
        var titleText = titleBlock.GetProperty("text").GetString();
        Assert.Contains("created", titleText!);
        Assert.Contains("Http", titleText!);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldIncludeCorrectPayloadForCheckUpdated()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var changes = new Dictionary<string, object>
        {
            ["Schedule"] = 300,
            ["TimeoutSeconds"] = 60
        };
        var context = CreateLifecycleContext(
            eventType: EventTypes.CheckUpdated,
            configurationChanges: changes);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendLifecycleEventAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body").EnumerateArray().ToList();

        var titleBlock = body[0];
        var titleText = titleBlock.GetProperty("text").GetString();
        Assert.Contains("updated", titleText!);

        var changeFieldsBlocks = body.Where(b =>
            b.TryGetProperty("text", out var text) &&
            text.GetString()!.Contains("Changed Fields")).ToList();
        Assert.AreNotEqual(0, changeFieldsBlocks.Count);

        var changeFieldsValueBlock = body.FirstOrDefault(b =>
            b.TryGetProperty("text", out var text) &&
            text.GetString()!.Contains("Schedule"));
        Assert.AreNotEqual(JsonValueKind.Undefined, changeFieldsValueBlock.ValueKind);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldIncludeCorrectPayloadForCheckDeleted()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateLifecycleContext(eventType: EventTypes.CheckDeleted);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendLifecycleEventAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body");

        var titleBlock = body[0];
        var titleText = titleBlock.GetProperty("text").GetString();
        Assert.Contains("deleted", titleText!);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldReturnFailureWhenWebhookUrlMissing()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Channel",
            ChannelType = "Teams",
            ConfigurationJson = [],
        };
        var context = CreateLifecycleContext();

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Webhook URL not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldHandleHttpRequestException()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateLifecycleContext();

        _testHandler.ExceptionToThrow = new HttpRequestException("Network error");

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("HTTP error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateLifecycleContext();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _handler.SendLifecycleEventAsync(channel, context, cts.Token);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Request cancelled", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldReturnSuccessWhenTeamsApiReturnsOk()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateStatusChangeEventContext();

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("1")
        };

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(_testHandler.RequestReceived);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldIncludeCorrectPayloadStructure()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateStatusChangeEventContext(
            checkName: "API Health Check",
            workspaceName: "Production",
            previousStatus: CheckStatuses.Up,
            newStatus: CheckStatuses.Down,
            responseTimeMs: 3000,
            errorMessage: "Connection timeout");

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsNotNull(_testHandler.RequestContent);
        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent);

        Assert.IsTrue(payload.TryGetProperty("type", out var typeElement));
        Assert.AreEqual("message", typeElement.GetString());

        Assert.IsTrue(payload.TryGetProperty("attachments", out var attachments));
        Assert.AreEqual(1, attachments.GetArrayLength());

        var attachment = attachments[0];
        Assert.AreEqual("application/vnd.microsoft.card.adaptive", attachment.GetProperty("contentType").GetString());

        var content = attachment.GetProperty("content");
        Assert.AreEqual("AdaptiveCard", content.GetProperty("type").GetString());
        Assert.AreEqual("1.4", content.GetProperty("version").GetString());
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldIncludeStatusChangeInTitle()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateStatusChangeEventContext(
            checkName: "Test Check",
            previousStatus: CheckStatuses.Warn,
            newStatus: CheckStatuses.Down);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusChangeEventAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body");
        var titleBlock = body[0];
        var titleText = titleBlock.GetProperty("text").GetString();

        Assert.Contains("status changed", titleText!);
        Assert.Contains("Test Check", titleText!);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldIncludeAllRelevantFacts()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateStatusChangeEventContext(
            previousStatus: CheckStatuses.Up,
            newStatus: CheckStatuses.Warn,
            responseTimeMs: 2500,
            errorMessage: "Slow response",
            workspaceName: "Staging");

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusChangeEventAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var content = payload.GetProperty("attachments")[0].GetProperty("content");
        var body = content.GetProperty("body").EnumerateArray().ToList();

        var subtitleBlock = body[1];
        var subtitleText = subtitleBlock.GetProperty("text").GetString();
        Assert.Contains("Up", subtitleText!);
        Assert.Contains("Warn", subtitleText!);

        var factSetBlock = body.FirstOrDefault(b => b.GetProperty("type").GetString() == "FactSet");
        Assert.AreNotEqual(JsonValueKind.Undefined, factSetBlock.ValueKind);

        var facts = factSetBlock.GetProperty("facts").EnumerateArray().ToList();
        var factTitles = facts.Select(f => f.GetProperty("title").GetString()).ToList();

        Assert.Contains("Response Time", factTitles);
        Assert.Contains("Previous Status", factTitles);
        Assert.Contains("New Status", factTitles);
        Assert.Contains("Workspace", factTitles);

        var responseTimeFact = facts.First(f => f.GetProperty("title").GetString() == "Response Time");
        Assert.AreEqual("2500ms", responseTimeFact.GetProperty("value").GetString());

        var errorLabelBlock = body.FirstOrDefault(b =>
            b.TryGetProperty("text", out var text) &&
            text.GetString()!.Contains("**Error:**"));
        Assert.AreNotEqual(JsonValueKind.Undefined, errorLabelBlock.ValueKind);

        var errorMessageBlocks = body.Where(b =>
            b.TryGetProperty("fontType", out var fontType) &&
            fontType.GetString() == "monospace").ToList();
        Assert.AreNotEqual(0, errorMessageBlocks.Count);

        var errorMessageText = errorMessageBlocks[0].GetProperty("text").GetString();
        Assert.Contains("Slow response", errorMessageText!);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldReturnFailureWhenWebhookUrlMissing()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Channel",
            ChannelType = "Teams",
            ConfigurationJson = [],
        };
        var context = CreateStatusChangeEventContext();

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Webhook URL not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldHandleHttpRequestException()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateStatusChangeEventContext();

        _testHandler.ExceptionToThrow = new HttpRequestException("Network error");

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("HTTP error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("https://outlook.office.com/webhook/TEST");
        var context = CreateStatusChangeEventContext();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _handler.SendStatusChangeEventAsync(channel, context, cts.Token);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Request cancelled", result.ErrorMessage);
    }

    private static NotificationChannel CreateChannel(string webhookUrl)
    {
        return new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Teams Channel",
            ChannelType = "Teams",
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.Webhook.WebhookUrl] = JsonSerializer.SerializeToElement(webhookUrl)
            }
        };
    }

    private static StatusAlertContext CreateContext(
        string checkName = "Test Check",
        string status = CheckStatuses.Down,
        string? errorMessage = null,
        int? responseTimeMs = null,
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

    private static LifecycleEventContext CreateLifecycleContext(
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
