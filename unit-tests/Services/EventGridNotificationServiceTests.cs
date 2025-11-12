using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestSama.Services
{
    [TestClass]
    public class EventGridNotificationServiceTests
    {
        private ILogger<EventGridNotificationService> _logger;
        private SettingsService _settings;
        private BackgroundExecutionWrapper _bgExec;
        private EventGridPublisherClientWrapper _eventGridWrapper;
        private EventGridNotificationService _service;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<ILogger<EventGridNotificationService>>();
            _settings = Substitute.For<SettingsService>((IServiceProvider)null);
            _bgExec = Substitute.For<BackgroundExecutionWrapper>();
            _eventGridWrapper = Substitute.For<EventGridPublisherClientWrapper>();

            _service = new EventGridNotificationService(_logger, _settings, _bgExec, _eventGridWrapper);

            // Configure the service with mock settings
            _settings.Notifications_EventGrid_TopicEndpoint.Returns("https://test-topic.eastus-1.eventgrid.azure.net/api/events");
            _settings.Notifications_EventGrid_AccessKey.Returns("test-access-key");
        }

        [TestMethod]
        public void NotifySingleResultShouldExecuteInBackground()
        {
            var endpoint = CreateTestHttpEndpoint();
            var result = new EndpointCheckResult 
            { 
                Start = DateTimeOffset.UtcNow,
                Stop = DateTimeOffset.UtcNow.AddSeconds(1),
                Success = true,
                ResponseTime = TimeSpan.FromMilliseconds(250)
            };

            _service.NotifySingleResult(endpoint, result);

            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void NotifyUpShouldExecuteInBackground()
        {
            var endpoint = CreateTestHttpEndpoint();
            var downAsOf = DateTimeOffset.UtcNow.AddMinutes(-10);

            _service.NotifyUp(endpoint, downAsOf);

            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void NotifyDownShouldExecuteInBackground()
        {
            var endpoint = CreateTestHttpEndpoint();
            var downAsOf = DateTimeOffset.UtcNow;
            var exception = new Exception("Connection timeout");

            _service.NotifyDown(endpoint, downAsOf, exception);

            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void NotifyMiscShouldExecuteInBackgroundForEndpointAdded()
        {
            var endpoint = CreateTestHttpEndpoint();

            _service.NotifyMisc(endpoint, NotificationType.EndpointAdded);

            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void NotifyMiscShouldExecuteInBackgroundForEndpointRemoved()
        {
            var endpoint = CreateTestHttpEndpoint();

            _service.NotifyMisc(endpoint, NotificationType.EndpointRemoved);

            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void NotifyMiscShouldExecuteInBackgroundForEndpointEnabled()
        {
            var endpoint = CreateTestHttpEndpoint();

            _service.NotifyMisc(endpoint, NotificationType.EndpointEnabled);

            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void NotifyMiscShouldExecuteInBackgroundForEndpointDisabled()
        {
            var endpoint = CreateTestHttpEndpoint();

            _service.NotifyMisc(endpoint, NotificationType.EndpointDisabled);

            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void NotifyMiscShouldExecuteInBackgroundForEndpointReconfigured()
        {
            var endpoint = CreateTestHttpEndpoint();

            _service.NotifyMisc(endpoint, NotificationType.EndpointReconfigured);

            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public async Task ShouldNotExecuteWhenTopicEndpointIsNotConfigured()
        {
            _settings.Notifications_EventGrid_TopicEndpoint.Returns((string)null);
            var endpoint = CreateTestHttpEndpoint();
            var result = new EndpointCheckResult { Success = true };

            // Capture the action passed to background execution
            Action capturedAction = null;
            _bgExec.When(x => x.Execute(Arg.Any<Action>())).Do(call => capturedAction = call.Arg<Action>());

            _service.NotifySingleResult(endpoint, result);

            // Should still call Execute, but the async function inside should return early
            _bgExec.Received(1).Execute(Arg.Any<Action>());
            
            // Execute the captured action synchronously
            capturedAction?.Invoke();

            await _eventGridWrapper.DidNotReceive().SendEventAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<EventGridEvent>()
            );
        }

        [TestMethod]
        public async Task ShouldNotExecuteWhenAccessKeyIsNotConfigured()
        {
            _settings.Notifications_EventGrid_AccessKey.Returns((string)null);
            var endpoint = CreateTestHttpEndpoint();
            var result = new EndpointCheckResult { Success = true };

            // Capture the action passed to background execution
            Action capturedAction = null;
            _bgExec.When(x => x.Execute(Arg.Any<Action>())).Do(call => capturedAction = call.Arg<Action>());

            _service.NotifySingleResult(endpoint, result);

            // Should still call Execute, but the async function inside should return early
            _bgExec.Received(1).Execute(Arg.Any<Action>());
            
            // Execute the captured action synchronously
            capturedAction?.Invoke();

            await _eventGridWrapper.DidNotReceive().SendEventAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<EventGridEvent>()
            );
        }

        [TestMethod]
        public async Task ShouldNotExecuteWhenTopicEndpointIsEmpty()
        {
            _settings.Notifications_EventGrid_TopicEndpoint.Returns("");
            var endpoint = CreateTestHttpEndpoint();
            var result = new EndpointCheckResult { Success = true };

            // Capture the action passed to background execution
            Action capturedAction = null;
            _bgExec.When(x => x.Execute(Arg.Any<Action>())).Do(call => capturedAction = call.Arg<Action>());

            _service.NotifySingleResult(endpoint, result);

            // Should still call Execute, but the async function inside should return early
            _bgExec.Received(1).Execute(Arg.Any<Action>());
            
            // Execute the captured action synchronously
            capturedAction?.Invoke();

            await _eventGridWrapper.DidNotReceive().SendEventAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<EventGridEvent>()
            );
        }

        [TestMethod]
        public async Task ShouldNotExecuteWhenAccessKeyIsEmpty()
        {
            _settings.Notifications_EventGrid_AccessKey.Returns("");
            var endpoint = CreateTestHttpEndpoint();
            var result = new EndpointCheckResult { Success = true };

            // Capture the action passed to background execution
            Action capturedAction = null;
            _bgExec.When(x => x.Execute(Arg.Any<Action>())).Do(call => capturedAction = call.Arg<Action>());

            _service.NotifySingleResult(endpoint, result);

            // Should still call Execute, but the async function inside should return early
            _bgExec.Received(1).Execute(Arg.Any<Action>());
            
            // Execute the captured action synchronously
            capturedAction?.Invoke();

            await _eventGridWrapper.DidNotReceive().SendEventAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Any<EventGridEvent>()
            );
        }

        private Endpoint CreateTestHttpEndpoint()
        {
            return TestUtility.CreateHttpEndpoint("test-endpoint", true, 1, "https://example.com");
        }

        [TestMethod]
        public async Task NotifySingleResultShouldSendCorrectEventPayload()
        {
            var endpoint = CreateTestHttpEndpoint();
            var startTime = DateTimeOffset.UtcNow;
            var stopTime = startTime.AddMilliseconds(250);
            var result = new EndpointCheckResult 
            { 
                Start = startTime,
                Stop = stopTime,
                Success = true,
                ResponseTime = TimeSpan.FromMilliseconds(250),
                Error = null
            };

            // Capture the action passed to background execution
            Action capturedAction = null;
            _bgExec.When(x => x.Execute(Arg.Any<Action>())).Do(call => capturedAction = call.Arg<Action>());

            _service.NotifySingleResult(endpoint, result);

            // Execute the captured action synchronously to trigger the wrapper call
            capturedAction?.Invoke();

            await _eventGridWrapper.Received(1).SendEventAsync(
                Arg.Is<Uri>(uri => uri.ToString() == "https://test-topic.eastus-1.eventgrid.azure.net/api/events"),
                Arg.Is<string>(key => key == "test-access-key"),
                Arg.Is<EventGridEvent>(evt => 
                    evt.EventType == "sama.endpoint.check.completed" &&
                    evt.Subject == "sama/endpoints/1" &&
                    evt.DataVersion == "1.0" &&
                    VerifyCheckCompletedEventData(evt.Data, endpoint, result)
                )
            );
        }

        [TestMethod]
        public async Task NotifyUpShouldSendCorrectEventPayload()
        {
            var endpoint = CreateTestHttpEndpoint();
            var downAsOf = DateTimeOffset.UtcNow.AddMinutes(-10);

            // Capture the action passed to background execution
            Action capturedAction = null;
            _bgExec.When(x => x.Execute(Arg.Any<Action>())).Do(call => capturedAction = call.Arg<Action>());

            _service.NotifyUp(endpoint, downAsOf);

            // Execute the captured action synchronously to trigger the wrapper call
            capturedAction?.Invoke();

            await _eventGridWrapper.Received(1).SendEventAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Is<EventGridEvent>(evt => 
                    evt.EventType == "sama.endpoint.status.up" &&
                    evt.Subject == "sama/endpoints/1" &&
                    evt.DataVersion == "1.0" &&
                    VerifyUpEventData(evt.Data, endpoint, downAsOf)
                )
            );
        }

        [TestMethod]
        public async Task NotifyDownShouldSendCorrectEventPayload()
        {
            var endpoint = CreateTestHttpEndpoint();
            var downAsOf = DateTimeOffset.UtcNow;
            var exception = new InvalidOperationException("Connection timeout");

            // Capture the action passed to background execution
            Action capturedAction = null;
            _bgExec.When(x => x.Execute(Arg.Any<Action>())).Do(call => capturedAction = call.Arg<Action>());

            _service.NotifyDown(endpoint, downAsOf, exception);

            // Execute the captured action synchronously to trigger the wrapper call
            capturedAction?.Invoke();

            await _eventGridWrapper.Received(1).SendEventAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Is<EventGridEvent>(evt => 
                    evt.EventType == "sama.endpoint.status.down" &&
                    evt.Subject == "sama/endpoints/1" &&
                    evt.DataVersion == "1.0" &&
                    VerifyDownEventData(evt.Data, endpoint, downAsOf, exception)
                )
            );
        }

        [TestMethod]
        public async Task NotifyMiscShouldSendCorrectEventPayloadForEndpointAdded()
        {
            var endpoint = CreateTestHttpEndpoint();

            // Capture the action passed to background execution
            Action capturedAction = null;
            _bgExec.When(x => x.Execute(Arg.Any<Action>())).Do(call => capturedAction = call.Arg<Action>());

            _service.NotifyMisc(endpoint, NotificationType.EndpointAdded);

            // Execute the captured action synchronously to trigger the wrapper call
            capturedAction?.Invoke();

            await _eventGridWrapper.Received(1).SendEventAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Is<EventGridEvent>(evt => 
                    evt.EventType == "sama.endpoint.management.added" &&
                    evt.Subject == "sama/endpoints/1" &&
                    evt.DataVersion == "1.0" &&
                    VerifyManagementEventData(evt.Data, endpoint, NotificationType.EndpointAdded)
                )
            );

        }

        [TestMethod]
        public async Task NotifyMiscShouldSendCorrectEventPayloadForEndpointRemoved()
        {
            var endpoint = CreateTestHttpEndpoint();

            // Capture the action passed to background execution
            Action capturedAction = null;
            _bgExec.When(x => x.Execute(Arg.Any<Action>())).Do(call => capturedAction = call.Arg<Action>());

            _service.NotifyMisc(endpoint, NotificationType.EndpointRemoved);

            // Execute the captured action synchronously to trigger the wrapper call
            capturedAction?.Invoke();

            await _eventGridWrapper.Received(1).SendEventAsync(
                Arg.Any<Uri>(),
                Arg.Any<string>(),
                Arg.Is<EventGridEvent>(evt => 
                    evt.EventType == "sama.endpoint.management.removed" &&
                    evt.Subject == "sama/endpoints/1" &&
                    VerifyManagementEventData(evt.Data, endpoint, NotificationType.EndpointRemoved)
                )
            );
        }

        private bool VerifyCheckCompletedEventData(BinaryData data, Endpoint endpoint, EndpointCheckResult result)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(data.ToString());
            
            return json.GetProperty("endpointId").GetInt32() == endpoint.Id &&
                   json.GetProperty("endpointName").GetString() == endpoint.Name &&
                   json.GetProperty("success").GetBoolean() == result.Success &&
                   json.GetProperty("responseTime").GetDouble() == result.ResponseTime?.TotalMilliseconds &&
                   json.GetProperty("startTime").GetDateTimeOffset() == result.Start &&
                   json.GetProperty("stopTime").GetDateTimeOffset() == result.Stop &&
                   json.GetProperty("error").ValueKind == JsonValueKind.Null;
        }

        private bool VerifyUpEventData(BinaryData data, Endpoint endpoint, DateTimeOffset? downAsOf)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(data.ToString());
            var expectedDowntimeMinutes = downAsOf.HasValue 
                ? (int)DateTimeOffset.UtcNow.Subtract(downAsOf.Value).TotalMinutes 
                : 0;
            
            return json.GetProperty("endpointId").GetInt32() == endpoint.Id &&
                   json.GetProperty("endpointName").GetString() == endpoint.Name &&
                   json.GetProperty("downAsOf").GetDateTimeOffset() == downAsOf &&
                   json.GetProperty("downtimeMinutes").GetInt32() == expectedDowntimeMinutes &&
                   json.TryGetProperty("recoveredAt", out var recoveredAt) && recoveredAt.ValueKind == JsonValueKind.String;
        }

        private bool VerifyDownEventData(BinaryData data, Endpoint endpoint, DateTimeOffset downAsOf, Exception exception)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(data.ToString());
            
            return json.GetProperty("endpointId").GetInt32() == endpoint.Id &&
                   json.GetProperty("endpointName").GetString() == endpoint.Name &&
                   json.GetProperty("downAsOf").GetDateTimeOffset() == downAsOf &&
                   json.GetProperty("reason").GetString() == exception.Message &&
                   json.GetProperty("reasonType").GetString() == exception.GetType().Name;
        }

        private bool VerifyManagementEventData(BinaryData data, Endpoint endpoint, NotificationType notificationType)
        {
            var json = JsonSerializer.Deserialize<JsonElement>(data.ToString());
            
            return json.GetProperty("endpointId").GetInt32() == endpoint.Id &&
                   json.GetProperty("endpointName").GetString() == endpoint.Name &&
                   json.GetProperty("notificationType").GetString() == notificationType.ToString() &&
                   json.TryGetProperty("timestamp", out var timestamp) && timestamp.ValueKind == JsonValueKind.String;
        }
    }
}
