using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Text;

namespace TestSama.Services
{
    [TestClass]
    public class GraphiteNotificationServiceTests
    {
        private ILogger<GraphiteNotificationService> _logger;
        private SettingsService _settings;
        private TcpClientWrapper _tcpWrapper;
        private GraphiteNotificationService _service;

        private string _sentData;

        [TestInitialize]
        public void Setup()
        {
            _logger = Substitute.For<ILogger<GraphiteNotificationService>>();
            _settings = Substitute.For<SettingsService>((IServiceProvider)null);
            _tcpWrapper = Substitute.For<TcpClientWrapper>();

            _service = new GraphiteNotificationService(_logger, _settings, _tcpWrapper);

            _settings.Notifications_Graphite_Host.Returns("asdf");
            _settings.Notifications_Graphite_Port.Returns(1234);

            _sentData = null;
            _tcpWrapper.When(t => t.SendData("asdf", 1234, Arg.Any<byte[]>()))
                .Do(ci =>
                {
                    var bytes = ci.Arg<byte[]>();
                    _sentData = Encoding.ASCII.GetString(bytes);
                });
        }

        [DataTestMethod]
        [DataRow("asdf123098FDSA", "asdf123098FDSA")]
        [DataRow("asdf123098_FDSA", "asdf123098-FDSA")]
        [DataRow("asdf123098-FDSA", "asdf123098-FDSA")]
        [DataRow("%Some  $  Thing!", "Some-Thing")]
        [DataRow("!*$^&#", "none")]
        public void ShouldCorrectlyFilterEndpointNamesForGraphite(string input, string expectedOutput)
        {
            _service.NotifySingleResult(new Endpoint { Name = input }, new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Success = false });

            var expectedSubstring = $"sama.{expectedOutput}.response.success ";
            StringAssert.StartsWith(_sentData, expectedSubstring);
        }

        [TestMethod]
        public void ShouldNotifySingleResultFailure()
        {
            var ep = new Endpoint { Name = "ep1" };
            var result = new EndpointCheckResult { Start = DateTimeOffset.Parse("2018-01-01T12:00:00.000"), Success = false };

            _service.NotifySingleResult(ep, result);

            Assert.AreEqual("sama.ep1.response.success 0 1514826000\n", _sentData);
        }

        [TestMethod]
        public void ShouldNotifySingleResultSuccessWithoutResponseTime()
        {
            var ep = new Endpoint { Name = "ep2" };
            var result = new EndpointCheckResult { Start = DateTimeOffset.Parse("2018-01-01T12:00:01.000"), Success = true };

            _service.NotifySingleResult(ep, result);

            Assert.AreEqual("sama.ep2.response.success 1 1514826001\n", _sentData);
        }

        [TestMethod]
        public void ShouldNotifySingleResultSuccessWithResponseTime()
        {
            var ep = new Endpoint { Name = "ep3" };
            var result = new EndpointCheckResult { Start = DateTimeOffset.Parse("2018-01-01T12:00:08.000"), Success = true, ResponseTime = TimeSpan.FromMilliseconds(54321) };

            _service.NotifySingleResult(ep, result);

            Assert.AreEqual("sama.ep3.response.success 1 1514826008\nsama.ep3.response.timeMsec 54321 1514826008\n", _sentData);
        }

        [TestMethod]
        public void ShouldNotNotifyWhenUnconfigured()
        {
            var ep = new Endpoint { Name = "epU" };
            var result = new EndpointCheckResult { Start = DateTimeOffset.Parse("2018-01-01T12:08:20.000"), Success = false };

            _settings.Notifications_Graphite_Host.Returns("");
            _settings.Notifications_Graphite_Port.Returns(1234);

            _service.NotifySingleResult(ep, result);

            _tcpWrapper.DidNotReceiveWithAnyArgs().SendData("", 0, Arg.Any<byte[]>());

            _settings.Notifications_Graphite_Host.Returns("asdf");
            _settings.Notifications_Graphite_Port.Returns(0);

            _service.NotifySingleResult(ep, result);

            _tcpWrapper.DidNotReceiveWithAnyArgs().SendData("", 0, Arg.Any<byte[]>());
        }
    }
}
