using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestSama.Services
{
    [TestClass]
    public class EndpointCheckServiceTests
    {
        private EndpointCheckService _service;
        private SettingsService _settingsService;
        private StateService _stateService;
        private SlackNotificationService _slackService;
        private TestHttpHandler _httpHandler;
        private IServiceProvider _serviceProvider;

        [TestInitialize]
        public void Setup()
        {
            _stateService = Substitute.For<StateService>();
            _settingsService = Substitute.For<SettingsService>((IServiceProvider)null);
            _slackService = Substitute.For<SlackNotificationService>(null, null, null);
            _serviceProvider = TestUtility.InitDI();
            _httpHandler = (TestHttpHandler)_serviceProvider.GetRequiredService<HttpClientHandler>();

            _service = new EndpointCheckService(_settingsService, _stateService, _slackService, _serviceProvider);

            _settingsService.Monitor_RequestTimeoutSeconds.Returns(1);
        }

        [TestMethod]
        public void CheckShouldNotSendSuccessMessageIfFirstTimeSuccess()
        {
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa" }, 0);

            _slackService.DidNotReceive().Notify(Arg.Any<Endpoint>(), Arg.Any<bool>(), Arg.Any<Exception>());
        }

        [TestMethod]
        public void CheckShouldNotSendSuccessMessageAfterPreviousSuccess()
        {
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = true });
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa" }, 0);

            _slackService.DidNotReceive().Notify(Arg.Any<Endpoint>(), Arg.Any<bool>(), Arg.Any<Exception>());
        }

        [TestMethod]
        public void CheckShouldSendSuccessMessageAfterSettingsChange()
        {
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = null });
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa" }, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), true, Arg.Any<Exception>());
        }

        [TestMethod]
        public void CheckShouldSendSuccessMessageAfterPreviousFailure()
        {
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = false });
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)));

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa" }, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), true, Arg.Any<Exception>());
        }

        [TestMethod]
        public void CheckShouldFailWhenKeywordMatchMissing()
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("wrong keywords here"));
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = null });
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa", ResponseMatch = "theKEY" }, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), false, Arg.Any<Exception>());
        }

        [TestMethod]
        public void CheckShouldSucceedWhenKeywordMatchSucceeds()
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("all of the keys are here"));
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = null });
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa", ResponseMatch = "the keys" }, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), true, Arg.Any<Exception>());
        }

        [TestMethod]
        public async Task CheckShouldRetryConfiguredNumberOfTimesBeforeFailing()
        {
            _settingsService.Monitor_MaxRetries.Returns(4);
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<HttpResponseMessage>(new Exception("ERROR")));

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa" }, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), false, Arg.Any<Exception>());
            await _httpHandler.Received(5).RealSendAsync(Arg.Is<HttpRequestMessage>(m => m.RequestUri.ToString() == "http://asdf.example.com/fdsa"), Arg.Any<CancellationToken>());
        }

        [TestMethod]
        public void CheckShouldSucceedWhenCustomStatusCodesSet()
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden);
            response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(""));
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = null });
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa", StatusCodes = " 403" }, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), true, Arg.Any<Exception>());
        }

        [TestMethod]
        public void CheckShouldSetAllowAutoRedirectForDefaultStatusCodes()
        {
            Assert.IsTrue(_httpHandler.AllowAutoRedirect);

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa", StatusCodes = "" }, 0);

            Assert.IsTrue(_httpHandler.AllowAutoRedirect);
        }

        [TestMethod]
        public void CheckShouldDisableAllowAutoRedirectForSpecifiedStatusCodes()
        {
            Assert.IsTrue(_httpHandler.AllowAutoRedirect);

            _service.ProcessEndpoint(new Endpoint { Name = "A", Location = "http://asdf.example.com/fdsa", StatusCodes = "403" }, 0);

            Assert.IsFalse(_httpHandler.AllowAutoRedirect);
        }
    }
}
