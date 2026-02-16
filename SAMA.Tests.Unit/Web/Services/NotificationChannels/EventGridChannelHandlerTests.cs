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
public class EventGridChannelHandlerTests
{
    private IHttpClientFactory _mockHttpClientFactory = null!;
    private HttpClient _httpClient = null!;
    private TestHttpMessageHandler _testHandler = null!;
    private GlobalSettingsService _mockGlobalSettings = null!;
    private ILogger<EventGridChannelHandler> _mockLogger = null!;
    private EventGridChannelHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockHttpClientFactory = Substitute.For<IHttpClientFactory>();
        _testHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_testHandler);
        _mockHttpClientFactory.CreateClient(Arg.Any<string>()).Returns(_httpClient);
        _mockGlobalSettings = Substitute.For<GlobalSettingsService>(null, null, null, null);
        _mockGlobalSettings.NotificationTimeoutSeconds.Returns(30);
        _mockLogger = Substitute.For<ILogger<EventGridChannelHandler>>();
        _handler = new EventGridChannelHandler(_mockHttpClientFactory, _mockGlobalSettings, _mockLogger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient.Dispose();
        _testHandler.Dispose();
    }

    #region SendStatusAlertAsync Tests

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnSuccessWhenEventGridApiReturnsOk()
    {
        var channel = CreateChannel("https://test-topic.region.eventgrid.azure.net/api/events", "test-access-key");
        var context = CreateStatusAlertContext(status: CheckStatuses.Down, isRecovery: false);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNotNull(_testHandler.RequestReceived);
        Assert.AreEqual("https://test-topic.region.eventgrid.azure.net/api/events", _testHandler.RequestReceived.RequestUri?.ToString());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldIncludeAccessKeyHeader()
    {
        var channel = CreateChannel("https://test-topic.region.eventgrid.azure.net/api/events", "my-secret-key");
        var context = CreateStatusAlertContext();

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsNotNull(_testHandler.RequestReceived);
        Assert.IsTrue(_testHandler.RequestReceived.Headers.Contains("aeg-sas-key"));
        var keyValues = _testHandler.RequestReceived.Headers.GetValues("aeg-sas-key");
        Assert.AreEqual("my-secret-key", keyValues.First());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldUseJsonContentType()
    {
        var channel = CreateChannel("https://test-topic.region.eventgrid.azure.net/api/events", "key");
        var context = CreateStatusAlertContext();

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsNotNull(_testHandler.RequestReceived);
        Assert.AreEqual("application/json", _testHandler.RequestReceived.Content?.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldSendEventGridEventSchemaForDownStatus()
    {
        var channel = CreateChannel("https://test-topic.region.eventgrid.azure.net/api/events", "key");
        var context = CreateStatusAlertContext(
            checkName: "API Health Check",
            status: CheckStatuses.Down,
            errorMessage: "Connection timeout",
            responseTimeMs: 5000,
            workspaceName: "Production Workspace",
            isRecovery: false,
            consecutiveFailures: 3);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsNotNull(_testHandler.RequestContent);
        var eventsArray = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent);
        Assert.AreEqual(JsonValueKind.Array, eventsArray.ValueKind);
        Assert.AreEqual(1, eventsArray.GetArrayLength());

        var eventGridEvent = eventsArray[0];
        Assert.IsTrue(eventGridEvent.TryGetProperty("id", out _));
        Assert.AreEqual("SAMA.Check.StatusAlert.Down", eventGridEvent.GetProperty("eventType").GetString());
        Assert.AreEqual("workspaces/Production Workspace/checks/API Health Check", eventGridEvent.GetProperty("subject").GetString());
        Assert.IsTrue(eventGridEvent.TryGetProperty("eventTime", out _));
        Assert.AreEqual("1.0", eventGridEvent.GetProperty("dataVersion").GetString());

        var data = eventGridEvent.GetProperty("data");
        Assert.AreEqual(context.CheckId.ToString(), data.GetProperty("checkId").GetString());
        Assert.AreEqual("API Health Check", data.GetProperty("checkName").GetString());
        Assert.AreEqual("Production Workspace", data.GetProperty("workspaceName").GetString());
        Assert.AreEqual(CheckStatuses.Down, data.GetProperty("status").GetString());
        Assert.IsFalse(data.GetProperty("isRecovery").GetBoolean());
        Assert.AreEqual(3, data.GetProperty("consecutiveFailures").GetInt32());
        Assert.AreEqual(5000, data.GetProperty("responseTimeMs").GetInt32());
        Assert.AreEqual("Connection timeout", data.GetProperty("errorMessage").GetString());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldSendEventGridEventSchemaForRecovery()
    {
        var channel = CreateChannel("https://test-topic.region.eventgrid.azure.net/api/events", "key");
        var context = CreateStatusAlertContext(
            status: CheckStatuses.Up,
            isRecovery: true,
            consecutiveFailures: 5);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusAlertAsync(channel, context);

        var eventsArray = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var eventGridEvent = eventsArray[0];

        Assert.AreEqual("SAMA.Check.StatusAlert.Recovery", eventGridEvent.GetProperty("eventType").GetString());

        var data = eventGridEvent.GetProperty("data");
        Assert.IsTrue(data.GetProperty("isRecovery").GetBoolean());
        Assert.AreEqual(5, data.GetProperty("consecutiveFailures").GetInt32());
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenTopicEndpointMissing()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Channel",
            ChannelType = ChannelTypes.EventGrid,
            ConfigurationJson = [],
        };
        var context = CreateStatusAlertContext();

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Topic endpoint not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenTopicEndpointEmpty()
    {
        var channel = CreateChannel("", "key");
        var context = CreateStatusAlertContext();

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Topic endpoint not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenAccessKeyMissing()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Channel",
            ChannelType = ChannelTypes.EventGrid,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.EventGrid.TopicEndpoint] = JsonSerializer.SerializeToElement("https://test.eventgrid.azure.net/api/events")
            }
        };
        var context = CreateStatusAlertContext();

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Access key not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenAccessKeyEmpty()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "");
        var context = CreateStatusAlertContext();

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Access key not configured", result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldReturnFailureWhenEventGridApiReturnsError()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var context = CreateStatusAlertContext();

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Invalid event schema")
        };

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("BadRequest", result.ErrorMessage!);
        Assert.Contains("Invalid event schema", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleHttpRequestException()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var context = CreateStatusAlertContext();

        _testHandler.ExceptionToThrow = new HttpRequestException("Network error");

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.Contains("HTTP error", result.ErrorMessage!);
        Assert.Contains("Network error", result.ErrorMessage!);
    }

    [TestMethod]
    public async Task SendStatusAlertAsyncShouldHandleCancellation()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var context = CreateStatusAlertContext();

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

        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var context = CreateStatusAlertContext();

        _testHandler.SimulatedDelay = TimeSpan.FromSeconds(60);

        var result = await _handler.SendStatusAlertAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.ErrorMessage);
        Assert.Contains("timeout", result.ErrorMessage);
    }

    #endregion

    #region SendLifecycleEventAsync Tests

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldReturnSuccessWhenEventGridApiReturnsOk()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var context = CreateLifecycleEventContext(eventType: EventTypes.CheckCreated);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldSendEventGridEventSchemaForCheckCreated()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var context = CreateLifecycleEventContext(
            eventType: EventTypes.CheckCreated,
            checkName: "API Health Check",
            checkType: CheckTypes.Http,
            workspaceName: "Production Workspace",
            performedBy: "admin@example.com");

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendLifecycleEventAsync(channel, context);

        var eventsArray = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var eventGridEvent = eventsArray[0];

        Assert.IsTrue(eventGridEvent.TryGetProperty("id", out _));
        Assert.AreEqual("SAMA.Check.CheckCreated", eventGridEvent.GetProperty("eventType").GetString());
        Assert.AreEqual("workspaces/Production Workspace/checks/API Health Check", eventGridEvent.GetProperty("subject").GetString());
        Assert.IsTrue(eventGridEvent.TryGetProperty("eventTime", out _));
        Assert.AreEqual("1.0", eventGridEvent.GetProperty("dataVersion").GetString());

        var data = eventGridEvent.GetProperty("data");
        Assert.AreEqual(context.CheckId.ToString(), data.GetProperty("checkId").GetString());
        Assert.AreEqual("API Health Check", data.GetProperty("checkName").GetString());
        Assert.AreEqual(CheckTypes.GetShortDisplayName(CheckTypes.Http), data.GetProperty("checkType").GetString());
        Assert.AreEqual("Production Workspace", data.GetProperty("workspaceName").GetString());
        Assert.AreEqual("admin@example.com", data.GetProperty("performedBy").GetString());
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldIncludeConfigurationChangesForCheckUpdated()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var changes = new Dictionary<string, object>
        {
            ["Schedule"] = 300,
            ["TimeoutSeconds"] = 60
        };
        var context = CreateLifecycleEventContext(
            eventType: EventTypes.CheckUpdated,
            configurationChanges: changes);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendLifecycleEventAsync(channel, context);

        var eventsArray = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var eventGridEvent = eventsArray[0];

        Assert.AreEqual("SAMA.Check.CheckUpdated", eventGridEvent.GetProperty("eventType").GetString());

        var data = eventGridEvent.GetProperty("data");
        var configChanges = data.GetProperty("configurationChanges");
        Assert.AreEqual(JsonValueKind.Array, configChanges.ValueKind);
        var changeKeys = configChanges.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Schedule", changeKeys);
        Assert.Contains("TimeoutSeconds", changeKeys);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldHandleNullConfigurationChanges()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var context = CreateLifecycleEventContext(configurationChanges: null);

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendLifecycleEventAsync(channel, context);

        var eventsArray = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var eventGridEvent = eventsArray[0];
        var data = eventGridEvent.GetProperty("data");

        Assert.AreEqual(JsonValueKind.Null, data.GetProperty("configurationChanges").ValueKind);
    }

    [TestMethod]
    public async Task SendLifecycleEventAsyncShouldReturnFailureWhenTopicEndpointMissing()
    {
        var channel = new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test Channel",
            ChannelType = ChannelTypes.EventGrid,
            ConfigurationJson = [],
        };
        var context = CreateLifecycleEventContext();

        var result = await _handler.SendLifecycleEventAsync(channel, context);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Topic endpoint not configured", result.ErrorMessage);
    }

    #endregion

    #region SendStatusChangeEventAsync Tests

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldReturnSuccessWhenEventGridApiReturnsOk()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var context = CreateStatusChangeEventContext();

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        var result = await _handler.SendStatusChangeEventAsync(channel, context);

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public async Task SendStatusChangeEventAsyncShouldSendEventGridEventSchema()
    {
        var channel = CreateChannel("https://test.eventgrid.azure.net/api/events", "key");
        var context = CreateStatusChangeEventContext(
            checkName: "API Check",
            workspaceName: "Staging",
            previousStatus: CheckStatuses.Up,
            newStatus: CheckStatuses.Down,
            responseTimeMs: 3000,
            errorMessage: "Timeout");

        _testHandler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK);

        await _handler.SendStatusChangeEventAsync(channel, context);

        var eventsArray = JsonSerializer.Deserialize<JsonElement>(_testHandler.RequestContent!);
        var eventGridEvent = eventsArray[0];

        Assert.IsTrue(eventGridEvent.TryGetProperty("id", out _));
        Assert.AreEqual("SAMA.Check.StatusChanged", eventGridEvent.GetProperty("eventType").GetString());
        Assert.AreEqual("workspaces/Staging/checks/API Check", eventGridEvent.GetProperty("subject").GetString());
        Assert.IsTrue(eventGridEvent.TryGetProperty("eventTime", out _));
        Assert.AreEqual("1.0", eventGridEvent.GetProperty("dataVersion").GetString());

        var data = eventGridEvent.GetProperty("data");
        Assert.AreEqual(context.CheckId.ToString(), data.GetProperty("checkId").GetString());
        Assert.AreEqual("API Check", data.GetProperty("checkName").GetString());
        Assert.AreEqual("Staging", data.GetProperty("workspaceName").GetString());
        Assert.AreEqual(CheckStatuses.Up, data.GetProperty("previousStatus").GetString());
        Assert.AreEqual(CheckStatuses.Down, data.GetProperty("newStatus").GetString());
        Assert.AreEqual(3000, data.GetProperty("responseTimeMs").GetInt32());
        Assert.AreEqual("Timeout", data.GetProperty("errorMessage").GetString());
    }

    #endregion

    #region Helper Methods

    private static NotificationChannel CreateChannel(string topicEndpoint, string accessKey)
    {
        return new NotificationChannel
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Test EventGrid Channel",
            ChannelType = ChannelTypes.EventGrid,
            ConfigurationJson = new Dictionary<string, JsonElement>
            {
                [ConfigurationKeys.EventGrid.TopicEndpoint] = JsonSerializer.SerializeToElement(topicEndpoint),
                [ConfigurationKeys.EventGrid.AccessKey] = JsonSerializer.SerializeToElement(accessKey)
            }
        };
    }

    private static StatusAlertContext CreateStatusAlertContext(
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

    #endregion
}
