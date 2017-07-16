using Microsoft.Extensions.Configuration;
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
        private ILogger<SlackNotificationService> _logger;
        private IConfigurationRoot _configRoot;
        private TestHttpHandler _httpHandler;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<ILogger<SlackNotificationService>>();
            _configRoot = Substitute.For<IConfigurationRoot>();
            _httpHandler = Substitute.ForPartsOf<TestHttpHandler>();

            _service = new SlackNotificationService(_logger, _configRoot, _httpHandler);

            _configRoot.GetSection("SAMA").Returns(GetSamaConfig());
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

        private IConfigurationSection GetSamaConfig()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "SAMA:SlackWebHook", "https://webhook.example.com/hook" },
                })
                .Build()
                .GetSection("SAMA");
        }
    }
}
