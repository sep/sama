using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestSama.Services
{
    [TestClass]
    public class HttpCheckServiceTests
    {
        private SettingsService _settingsService;
        private IConfiguration _configuration;
        private CertificateValidationService _certService;
        private HttpHandlerFactory _httpHandlerFactory;
        private TestHttpHandler _httpHandler;
        private HttpCheckService _service;

        [TestInitialize]
        public void Setup()
        {
            _settingsService = Substitute.For<SettingsService>((IServiceProvider)null);
            _configuration = Substitute.For<IConfiguration>();
            _certService = Substitute.For<CertificateValidationService>(_settingsService);
            _httpHandlerFactory = Substitute.For<HttpHandlerFactory>();
            _httpHandler = Substitute.ForPartsOf<TestHttpHandler>();
            _httpHandlerFactory.Create(true, null).ReturnsForAnyArgs(_httpHandler);

            _service = new HttpCheckService(_settingsService, _certService, _configuration, _httpHandlerFactory);

            _settingsService.Monitor_RequestTimeoutSeconds.Returns(1);
        }

        [TestMethod]
        public void ShouldOnlyHandleHttpEndpoints()
        {
            Assert.IsTrue(_service.CanHandle(new Endpoint { Kind = Endpoint.EndpointKind.Http }));
            Assert.IsFalse(_service.CanHandle(new Endpoint { Kind = Endpoint.EndpointKind.Icmp }));
        }

        [TestMethod]
        public void CheckShouldFailWhenKeywordMatchMissing()
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("wrong keywords here"));
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));

            var result = _service.Check(TestUtility.CreateHttpEndpoint("A", httpLocation: "http://asdf.example.com/fdsa", httpResponseMatch: "theKEY"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual("The keyword match was not found", result.Error.Message);
        }

        [TestMethod]
        public void CheckShouldSucceedWhenKeywordMatchSucceeds()
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes("all of the keys are here"));
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));

            var result = _service.Check(TestUtility.CreateHttpEndpoint("A", httpLocation: "http://asdf.example.com/fdsa", httpResponseMatch: "the keys"));

            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public void CheckShouldSucceedWhenCustomStatusCodesSet()
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden);
            response.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(""));
            _httpHandler.RealSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(response));

            var result = _service.Check(TestUtility.CreateHttpEndpoint("A", httpLocation: "http://asdf.example.com/fdsa", httpStatusCodes: new List<int> { 403 }));

            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public void CheckShouldSetAllowAutoRedirectForDefaultStatusCodes()
        {
            _service.Check(TestUtility.CreateHttpEndpoint("A", httpLocation: "http://asdf.example.com/fdsa", httpStatusCodes: new List<int>()));

            _httpHandlerFactory.Received(0).Create(false, Arg.Any<SslClientAuthenticationOptions>());
            _httpHandlerFactory.Received(1).Create(true, Arg.Any<SslClientAuthenticationOptions>());
        }

        [TestMethod]
        public void CheckShouldDisableAllowAutoRedirectForSpecifiedStatusCodes()
        {
            _service.Check(TestUtility.CreateHttpEndpoint("A", httpLocation: "http://asdf.example.com/fdsa", httpStatusCodes: new List<int> { 403 }));

            _httpHandlerFactory.Received(1).Create(false, Arg.Any<SslClientAuthenticationOptions>());
            _httpHandlerFactory.Received(0).Create(true, Arg.Any<SslClientAuthenticationOptions>());
        }
    }
}
