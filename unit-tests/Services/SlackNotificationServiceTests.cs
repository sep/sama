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

            _service.Notify(new Endpoint { Name = "A" }, true, null);

            await _httpHandler.Received(1).RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
            Assert.AreEqual("https://webhook.example.com/hook", message.RequestUri.ToString());
            Assert.AreEqual(@"{""text"":""The endpoint 'A' is up. Hooray!""}", await message.Content.ReadAsStringAsync());
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

            _service.Notify(new Endpoint { Name = "A" }, false, new Exception("TESTERROR"));

            await _httpHandler.Received(1).RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>());
            Assert.AreEqual("https://webhook.example.com/hook", message.RequestUri.ToString());
            Assert.AreEqual(@"{""text"":""The endpoint 'A' is down: TESTERROR""}", await message.Content.ReadAsStringAsync());
        }
    }
}
