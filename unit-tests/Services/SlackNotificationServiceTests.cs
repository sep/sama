using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TestSama.Services
{
    [TestClass]
    public class SlackNotificationServiceTests
    {
        private SlackNotificationService _service;
        private ILogger<SlackNotificationService> _logger;
        private SettingsService _settings;
        private TestHttpHandler _httpHandler;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<ILogger<SlackNotificationService>>();
            _settings = Substitute.For<SettingsService>((IServiceProvider)null);
            _httpHandler = Substitute.ForPartsOf<TestHttpHandler>();

            _service = new SlackNotificationService(_logger, _settings, _httpHandler);

            _settings.Notifications_Slack_WebHook.Returns("https://webhook.example.com/hook");
        }

        [TestMethod]
        public async Task ShouldNotifyEndpointUpEvent()
        {
            HttpRequestMessage message = null;
            _httpHandler.When(h => h.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()))
                .Do(ci =>
                {
                    message = ci.Arg<HttpRequestMessage>();
                });

            _service.NotifyUp(new Endpoint { Name = "A" }, null);

            await _httpHandler.Received(1).RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
            Assert.AreEqual("https://webhook.example.com/hook", message.RequestUri.ToString());
            Assert.AreEqual(@"{""text"":""The endpoint 'A' is up. Hooray!""}", await message.Content.ReadAsStringAsync());
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
            Assert.AreEqual(@"{""text"":""The endpoint 'A' is up after being down for 5 hours. Hooray!""}", await message.Content.ReadAsStringAsync());
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
            Assert.AreEqual(@"{""text"":""The endpoint 'A' is down: TESTERROR!""}", await message.Content.ReadAsStringAsync());
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
            Assert.AreEqual(@"{""text"":""The endpoint 'A' is down: error1.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyDown(new Endpoint { Name = "A" }, DateTimeOffset.UtcNow, new Exception("error2."));
            Assert.AreEqual(@"{""text"":""The endpoint 'A' is down: error2.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyDown(new Endpoint { Name = "A" }, DateTimeOffset.UtcNow, new Exception("error3!"));
            Assert.AreEqual(@"{""text"":""The endpoint 'A' is down: error3!""}", await message.Content.ReadAsStringAsync());

            _service.NotifyDown(new Endpoint { Name = "A" }, DateTimeOffset.UtcNow, new Exception("error4?"));
            Assert.AreEqual(@"{""text"":""The endpoint 'A' is down: error4?""}", await message.Content.ReadAsStringAsync());
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
            Assert.AreEqual(@"{""text"":""The endpoint 'A' has been added.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointRemoved);
            Assert.AreEqual(@"{""text"":""The endpoint 'A' has been removed.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointEnabled);
            Assert.AreEqual(@"{""text"":""The endpoint 'A' has been enabled.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointDisabled);
            Assert.AreEqual(@"{""text"":""The endpoint 'A' has been disabled.""}", await message.Content.ReadAsStringAsync());

            _service.NotifyMisc(new Endpoint { Name = "A" }, NotificationType.EndpointReconfigured);
            Assert.AreEqual(@"{""text"":""The endpoint 'A' has been reconfigured.""}", await message.Content.ReadAsStringAsync());
        }
    }
}
