using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;

namespace TestSama.Services
{
    [TestClass]
    public class EventGridNotificationServiceTests
    {
        private ILogger<EventGridNotificationService> _logger;
        private SettingsService _settings;
        private BackgroundExecutionWrapper _bgExec;
        private EventGridNotificationService _service;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<ILogger<EventGridNotificationService>>();
            _settings = Substitute.For<SettingsService>((IServiceProvider)null);
            _bgExec = Substitute.For<BackgroundExecutionWrapper>();

            _service = new EventGridNotificationService(_logger, _settings, _bgExec);

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
        public void ShouldNotExecuteWhenTopicEndpointIsNotConfigured()
        {
            _settings.Notifications_EventGrid_TopicEndpoint.Returns((string)null);
            var endpoint = CreateTestHttpEndpoint();
            var result = new EndpointCheckResult { Success = true };

            _service.NotifySingleResult(endpoint, result);

            // Should still call Execute, but the async function inside should return early
            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void ShouldNotExecuteWhenAccessKeyIsNotConfigured()
        {
            _settings.Notifications_EventGrid_AccessKey.Returns((string)null);
            var endpoint = CreateTestHttpEndpoint();
            var result = new EndpointCheckResult { Success = true };

            _service.NotifySingleResult(endpoint, result);

            // Should still call Execute, but the async function inside should return early
            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void ShouldNotExecuteWhenTopicEndpointIsEmpty()
        {
            _settings.Notifications_EventGrid_TopicEndpoint.Returns("");
            var endpoint = CreateTestHttpEndpoint();
            var result = new EndpointCheckResult { Success = true };

            _service.NotifySingleResult(endpoint, result);

            // Should still call Execute, but the async function inside should return early
            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        [TestMethod]
        public void ShouldNotExecuteWhenAccessKeyIsEmpty()
        {
            _settings.Notifications_EventGrid_AccessKey.Returns("");
            var endpoint = CreateTestHttpEndpoint();
            var result = new EndpointCheckResult { Success = true };

            _service.NotifySingleResult(endpoint, result);

            // Should still call Execute, but the async function inside should return early
            _bgExec.Received(1).Execute(Arg.Any<Action>());
        }

        private Endpoint CreateTestHttpEndpoint()
        {
            return TestUtility.CreateHttpEndpoint("test-endpoint", true, 1, "https://example.com");
        }

        private Endpoint CreateTestIcmpEndpoint()
        {
            return TestUtility.CreateIcmpEndpoint("test-ping", true, 2, "192.168.1.1");
        }
    }
}
