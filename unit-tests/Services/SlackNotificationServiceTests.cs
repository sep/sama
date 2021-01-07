using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TestSama.Services
{
    [TestClass]
    public class SlackNotificationServiceTests
    {
        private SlackNotificationService _service;
        private IServiceProvider _provider;
        private ILogger<SlackNotificationService> _logger;
        private SettingsService _settings;
        private TestHttpHandler _httpHandler;
        private BackgroundExecutionWrapper _bgExec;

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _logger = Substitute.For<ILogger<SlackNotificationService>>();
            _settings = Substitute.For<SettingsService>((IServiceProvider)null);
            _httpHandler = _provider.GetRequiredService<HttpClientHandler>() as TestHttpHandler;
            _bgExec = Substitute.For<BackgroundExecutionWrapper>();

            _service = new SlackNotificationService(_logger, _settings, _provider, _bgExec);

            _settings.Notifications_Slack_WebHook.Returns("https://webhook.example.com/hook");
        }

        [TestMethod]
        public async Task ShouldBatchInitialUpEventNotifications()
        {
            var queuedActions = new List<Action>();
            _bgExec.When(bge => bge.ExecuteDelayed(Arg.Any<Action>(), 2500))
                .Do(ci =>
                {
                    queuedActions.Add(ci.Arg<Action>());
                });
            HttpRequestMessage message = null;
            _httpHandler.When(h => h.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()))
                .Do(ci =>
                {
                    message = ci.Arg<HttpRequestMessage>();
                });

            _service.NotifyUp(new Endpoint { Name = "A" }, null);
            _service.NotifyUp(new Endpoint { Name = "B" }, null);
            _service.NotifyUp(new Endpoint { Name = "C" }, null);

            queuedActions.ForEach(a => a.Invoke());

            await _httpHandler.Received(1).RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
            Assert.AreEqual("https://webhook.example.com/hook", message.RequestUri.ToString());
            Assert.AreEqual(@"{""text"":""The following endpoints are up: `A`, `B`, `C`. Hooray!""}", await message.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task ShouldNotifyEndpointUpEvent()
        {
            _bgExec.When(bge => bge.ExecuteDelayed(Arg.Any<Action>(), 2500))
                .Do(ci =>
                {
                    Action action = ci.Arg<Action>();
                    action.Invoke();
                });
            HttpRequestMessage message = null;
            _httpHandler.When(h => h.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()))
                .Do(ci =>
                {
                    message = ci.Arg<HttpRequestMessage>();
                });

            _service.NotifyUp(new Endpoint { Name = "A" }, null);

            await _httpHandler.Received(1).RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
            Assert.AreEqual("https://webhook.example.com/hook", message.RequestUri.ToString());
            Assert.AreEqual(@"{""text"":""The endpoint `A` is up. Hooray!""}", await message.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task ShouldNotifyEndpointUpEventWithDowntimeInfo()
        {
            HttpRequestMessage message = null;
            _httpHandler.When(h => h.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()))
                .Do(ci =>
                {
                    message = ci.Arg<HttpRequestMessage>();
                });

            _service.NotifyUp(new Endpoint { Name = "A" }, DateTimeOffset.UtcNow.AddHours(-5));

            await _httpHandler.Received(1).RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
            Assert.AreEqual("https://webhook.example.com/hook", message.RequestUri.ToString());
            Assert.AreEqual(@"{""text"":""The endpoint `A` is up after being down for 5 hours. Hooray!""}", await message.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task ShouldNotifyEndpointDownEvent()
        {
            HttpRequestMessage message = null;
            _httpHandler.When(h => h.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()))
                .Do(ci =>
                {
                    message = ci.Arg<HttpRequestMessage>();
                });

            _service.NotifyDown(new Endpoint { Name = "A" }, DateTimeOffset.UtcNow, new Exception("TESTERROR!"));

            await _httpHandler.Received(1).RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
            Assert.AreEqual("https://webhook.example.com/hook", message.RequestUri.ToString());
            Assert.AreEqual(@"{""text"":""The endpoint `A` is down: TESTERROR!""}", await message.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task ShouldAddPeriodToDownEventMessageOnlyWhenNoEndingPunctuationExists()
        {
            HttpRequestMessage message = null;
            _httpHandler.When(h => h.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()))
                .Do(ci =>
                {
                    message = ci.Arg<HttpRequestMessage>();
                });

            _service.NotifyDown(new Endpoint { Name = "A" }, DateTimeOffset.UtcNow, new Exception("error1"));
            Assert.AreEqual(@"{""text"":""The endpoint `A` is down: error1.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyDown(new Endpoint { Name = "A" }, DateTimeOffset.UtcNow, new Exception("error2."));
            Assert.AreEqual(@"{""text"":""The endpoint `A` is down: error2.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyDown(new Endpoint { Name = "A" }, DateTimeOffset.UtcNow, new Exception("error3!"));
            Assert.AreEqual(@"{""text"":""The endpoint `A` is down: error3!""}", await message.Content.ReadAsStringAsync());

            _service.NotifyDown(new Endpoint { Name = "A" }, DateTimeOffset.UtcNow, new Exception("error4?"));
            Assert.AreEqual(@"{""text"":""The endpoint `A` is down: error4?""}", await message.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task ShouldNotifyEndpointMiscEvents()
        {
            HttpRequestMessage message = null;
            _httpHandler.When(h => h.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()))
                .Do(ci =>
                {
                    message = ci.Arg<HttpRequestMessage>();
                });

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointAdded);
            Assert.AreEqual(@"{""text"":""The endpoint `A` has been added and will be checked shortly.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointRemoved);
            Assert.AreEqual(@"{""text"":""The endpoint `A` has been removed.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointEnabled);
            Assert.AreEqual(@"{""text"":""The endpoint `A` has been enabled and will be checked shortly.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointDisabled);
            Assert.AreEqual(@"{""text"":""The endpoint `A` has been disabled.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointReconfigured);
            Assert.AreEqual(@"{""text"":""The endpoint `A` has been reconfigured and will be checked shortly.""}", await message.Content.ReadAsStringAsync());
        }

        [TestMethod]
        public async Task ShouldNotSendNotificationsWhenWebhookUrlIsEmpty()
        {
            _settings.Notifications_Slack_WebHook.Returns("");

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointAdded);

            await _httpHandler.DidNotReceiveWithAnyArgs().RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
            _logger.DidNotReceiveWithAnyArgs().Log<object>(Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), (a, b) => "");
        }
    }
}
