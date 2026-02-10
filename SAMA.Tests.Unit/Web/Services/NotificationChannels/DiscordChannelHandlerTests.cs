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
public class DiscordChannelHandlerTests
{
    private IHttpClientFactory _mockHttpClientFactory = null!;
    private HttpClient _httpClient = null!;
    private TestHttpMessageHandler _testHandler = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private ILogger<DiscordChannelHandler> _mockLogger = null!;
    private DiscordChannelHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        _testHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_testHandler);
        _mockHttpClientFactory.CreateClient(Arg.Any<string>()).Returns(_httpClient);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null, null);
        _mockGlobalSettings.NotificationTimeoutSeconds.Returns(30);
        _mockLogger = Substitute.For<ILogger<DiscordChannelHandler>>();
        _handler = new DiscordChannelHandler(_mockHttpClientFactory, _mockGlobalSettings, _mockLogger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient.Dispose();
        _testHandler.Dispose();
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnSuccessWhenDiscordApiReturnsOk()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(status: CheckStatuses.Down, isRecovery: false);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(_testHandler.RequestReceived);
        Assert.AreEqual("https://discord.com/api/webhooks/TEST", _testHandler.RequestReceived.RequestUri?.ToString());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldIncludeCorrectPayloadStructure()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(
            checkName: "API Health Check",
            status: CheckStatuses.Down,
            errorMessage: "Connection timeout",
            responseTimeMs: 5000,
            workspaceName: "Production",
            isRecovery: false,
            consecutiveFailures: 3);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsNotNull(_testHandler.RequestContent);
        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent);

        Assert.IsTrue(payload.TryGetProperty("embeds", out var embeds));
        Assert.AreEqual(1, embeds.GetArrayLength());

        var embed = embeds[0];
        Assert.IsTrue(embed.TryGetProperty("title", out _));
        Assert.IsTrue(embed.TryGetProperty("description", out _));
        Assert.IsTrue(embed.TryGetProperty("color", out _));
        Assert.IsTrue(embed.TryGetProperty("fields", out _));
        Assert.IsTrue(embed.TryGetProperty("footer", out _));
        Assert.IsTrue(embed.TryGetProperty("timestamp", out _));
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldUseGoodColorForUpStatus()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(status: CheckStatuses.Up, isRecovery: false);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];

        Assert.AreEqual(0x2eb886, embed.GetProperty("color").GetInt32());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldUseWarningColorForWarnStatus()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(status: CheckStatuses.Warn, isRecovery: false);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];

        Assert.AreEqual(0xdaa038, embed.GetProperty("color").GetInt32());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldUseDangerColorForDownStatus()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(status: CheckStatuses.Down, isRecovery: false);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];

        Assert.AreEqual(0xa30200, embed.GetProperty("color").GetInt32());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldIndicateRecoveryInTitle()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(checkName: "Test Check", status: CheckStatuses.Up, isRecovery: true);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];
        var title = embed.GetProperty("title").GetString();

        Assert.Contains("back up", title!);
        Assert.Contains("Test Check", title!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldIncludeAllRelevantFields()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(
            status: CheckStatuses.Down,
            errorMessage: "Connection refused",
            responseTimeMs: 1500,
            workspaceName: "Staging",
            consecutiveFailures: 2);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];
        var fields = embed.GetProperty("fields").EnumerateArray().ToList();

        var fieldNames = fields.Select(f => f.GetProperty("name").GetString()).ToList();
        Assert.Contains("Response Time", fieldNames);
        Assert.Contains("Status", fieldNames);
        Assert.Contains("Workspace", fieldNames);
        Assert.Contains("Error", fieldNames);

        var responseTimeField = fields.First(f => f.GetProperty("name").GetString() == "Response Time");
        Assert.AreEqual("1500ms", responseTimeField.GetProperty("value").GetString());

        var errorField = fields.First(f => f.GetProperty("name").GetString() == "Error");
        var errorValue = errorField.GetProperty("value").GetString();
        Assert.Contains("Connection refused", errorValue!);

        var description = embed.GetProperty("description").GetString();
        Assert.Contains("2 consecutive", description!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenWebhookUrlMissing()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Channel",
            ChannelType = "Discord",
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
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenDiscordApiReturnsError()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
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
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
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
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
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

        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
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
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext();

        _testHandler.ExceptionToThrow = new InvalidOperationException("Unexpected error");

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("Unexpected error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldSetSentAtTimestamp()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext();

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        var before = DateTimeOffset.UtcNow;
        var result = await _handler.SendStatusAlertAsync(channel, context);
        var after = DateTimeOffset.UtcNow;

        Assert.IsTrue(result.SentAt >= before && result.SentAt <= after);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldTruncateLongErrorMessages()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var longError = new string('X', 1500);
        var context = CreateContext(errorMessage: longError);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];
        var fields = embed.GetProperty("fields").EnumerateArray().ToList();
        var errorField = fields.First(f => f.GetProperty("name").GetString() == "Error");
        var errorValue = errorField.GetProperty("value").GetString()!;

        Assert.IsLessThan(longError.Length + 10, errorValue.Length);
        Assert.Contains("...", errorValue);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldDisplayTwentyPlusWhenConsecutiveFailuresIs20()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(
            status: CheckStatuses.Down,
            consecutiveFailures: 20);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];
        var description = embed.GetProperty("description").GetString();

        Assert.Contains("20+", description!);
        Assert.DoesNotContain("**20 consecutive", description!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldDisplayExactCountWhenConsecutiveFailuresIsBelow20()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(
            status: CheckStatuses.Down,
            consecutiveFailures: 15);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];
        var description = embed.GetProperty("description").GetString();

        Assert.Contains("15 consecutive", description!);
        Assert.DoesNotContain("20+", description!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldDisplayTwentyPlusForRecoveryWhenConsecutiveFailuresWas20()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateContext(
            status: CheckStatuses.Up,
            isRecovery: true,
            consecutiveFailures: 20);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusAlertAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];
        var description = embed.GetProperty("description").GetString();

        Assert.Contains("20+", description!);
        Assert.DoesNotContain("**20 consecutive", description!);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldReturnSuccessWhenDiscordApiReturnsOk()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateLifecycleContext(eventType: EventTypes.CheckCreated);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(_testHandler.RequestReceived);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldIncludeCorrectPayloadForCheckCreated()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateLifecycleContext(
            eventType: EventTypes.CheckCreated,
            checkName: "API Health Check",
            checkType: "Http",
            workspaceName: "Production",
            performedBy: "admin@example.com");

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsNotNull(_testHandler.RequestContent);
        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent);

        Assert.IsTrue(payload.TryGetProperty("embeds", out var embeds));
        Assert.AreEqual(1, embeds.GetArrayLength());

        var embed = embeds[0];
        Assert.AreEqual(0x2eb886, embed.GetProperty("color").GetInt32());

        var title = embed.GetProperty("title").GetString();
        Assert.Contains("created", title!);
        Assert.Contains("Http", title!);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldIncludeCorrectPayloadForCheckUpdated()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var changes = new Dictionary<string, object>
        {
            ["Schedule"] = 300,
            ["TimeoutSeconds"] = 60
        };
        var context = CreateLifecycleContext(
            eventType: EventTypes.CheckUpdated,
            configurationChanges: changes);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendLifecycleEventAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];

        Assert.AreEqual(0x439FE0, embed.GetProperty("color").GetInt32());

        var title = embed.GetProperty("title").GetString();
        Assert.Contains("updated", title!);

        var fields = embed.GetProperty("fields").EnumerateArray().ToList();
        var changedFieldsField = fields.FirstOrDefault(f => f.GetProperty("name").GetString() == "Changed Fields");
        Assert.AreNotEqual(JsonValueKind.Undefined, changedFieldsField.ValueKind);

        var changedFieldsValue = changedFieldsField.GetProperty("value").GetString();
        Assert.Contains("Schedule", changedFieldsValue!);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldIncludeCorrectPayloadForCheckDeleted()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateLifecycleContext(eventType: EventTypes.CheckDeleted);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendLifecycleEventAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];

        Assert.AreEqual(0xa30200, embed.GetProperty("color").GetInt32());

        var title = embed.GetProperty("title").GetString();
        Assert.Contains("deleted", title!);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldReturnFailureWhenWebhookUrlMissing()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Channel",
            ChannelType = "Discord",
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
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateLifecycleContext();

        _testHandler.ExceptionToThrow = new HttpRequestException("Network error");

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("HTTP error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateLifecycleContext();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _handler.SendLifecycleEventAsync(channel, context, cts.Token);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Request cancelled", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldReturnSuccessWhenDiscordApiReturnsOk()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateStatusChangeEventContext();

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(_testHandler.RequestReceived);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldIncludeCorrectPayloadStructure()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateStatusChangeEventContext(
            checkName: "API Health Check",
            workspaceName: "Production",
            previousStatus: CheckStatuses.Up,
            newStatus: CheckStatuses.Down,
            responseTimeMs: 3000,
            errorMessage: "Connection timeout");

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsNotNull(_testHandler.RequestContent);
        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent);

        Assert.IsTrue(payload.TryGetProperty("embeds", out var embeds));
        Assert.AreEqual(1, embeds.GetArrayLength());

        var embed = embeds[0];
        Assert.IsTrue(embed.TryGetProperty("title", out _));
        Assert.IsTrue(embed.TryGetProperty("description", out _));
        Assert.IsTrue(embed.TryGetProperty("color", out _));
        Assert.IsTrue(embed.TryGetProperty("fields", out _));
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldUseCorrectColorForNewStatus()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateStatusChangeEventContext(
            previousStatus: CheckStatuses.Up,
            newStatus: CheckStatuses.Down);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusChangeEventAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];

        Assert.AreEqual(0xa30200, embed.GetProperty("color").GetInt32());
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldIncludeAllRelevantFields()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateStatusChangeEventContext(
            previousStatus: CheckStatuses.Warn,
            newStatus: CheckStatuses.Down,
            responseTimeMs: 2500,
            errorMessage: "Service unavailable",
            workspaceName: "Staging");

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.NoContent);

        await _handler.SendStatusChangeEventAsync(channel, context);

        var payload = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var embed = payload.GetProperty("embeds")[0];
        var fields = embed.GetProperty("fields").EnumerateArray().ToList();

        var fieldNames = fields.Select(f => f.GetProperty("name").GetString()).ToList();
        Assert.Contains("Response Time", fieldNames);
        Assert.Contains("Previous Status", fieldNames);
        Assert.Contains("New Status", fieldNames);
        Assert.Contains("Workspace", fieldNames);
        Assert.Contains("Error", fieldNames);

        var description = embed.GetProperty("description").GetString();
        Assert.Contains("Warn", description!);
        Assert.Contains("Down", description!);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldReturnFailureWhenWebhookUrlMissing()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Channel",
            ChannelType = "Discord",
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
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
        var context = CreateStatusChangeEventContext();

        _testHandler.ExceptionToThrow = new HttpRequestException("Network error");

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("HTTP error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("https://discord.com/api/webhooks/TEST");
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
            Name = "Test Discord Channel",
            ChannelType = "Discord",
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
