using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestSama.Services
{
    [TestClass]
    public class EndpointProcessServiceTests
    {
        private EndpointProcessService _service;
        private IServiceProvider _provider;
        private SettingsService _settingsService;
        private StateService _stateService;
        private ICheckService _goodCheckService;
        private ICheckService _badCheckService1;
        private ICheckService _badCheckService2;
        private Endpoint _validEndpoint;

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _settingsService = Substitute.For<SettingsService>((IServiceProvider)null);
            _stateService = Substitute.For<StateService>(_provider, null);
            _goodCheckService = Substitute.For<ICheckService>();
            _badCheckService1 = Substitute.For<ICheckService>();
            _badCheckService2 = Substitute.For<ICheckService>();

            _service = new EndpointProcessService(_settingsService, _stateService, new List<ICheckService> { _badCheckService1, _goodCheckService, _badCheckService2 }, _provider);

            _goodCheckService.CanHandle(Arg.Any<Endpoint>()).Returns(true);
            _badCheckService1.CanHandle(Arg.Any<Endpoint>()).Returns(false);
            _badCheckService2.CanHandle(Arg.Any<Endpoint>()).Returns(false);

            _validEndpoint = TestUtility.CreateHttpEndpoint("A", true, 0, "http://asdf");
            _validEndpoint.LastUpdated = DateTimeOffset.UtcNow;
            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                dbContext.Endpoints.Add(_validEndpoint);
                dbContext.SaveChanges();
            }
        }

        [TestMethod]
        public void ShouldSelectFirstMatchingCheckService()
        {
            _service.ProcessEndpoint(_validEndpoint, 0);

            _goodCheckService.Received().CanHandle(Arg.Any<Endpoint>());
            _badCheckService1.Received().CanHandle(Arg.Any<Endpoint>());
            _badCheckService2.DidNotReceive().CanHandle(Arg.Any<Endpoint>());

            _goodCheckService.Received().Check(Arg.Any<Endpoint>());
            _badCheckService1.DidNotReceive().Check(Arg.Any<Endpoint>());
            _badCheckService2.DidNotReceive().Check(Arg.Any<Endpoint>());
        }

        [TestMethod]
        public void ShouldSendFailureIfNoMatchingCheckServices()
        {
            _goodCheckService.CanHandle(Arg.Any<Endpoint>()).Returns(false);
            _service.ProcessEndpoint(_validEndpoint, 0);

            _stateService.Received().AddEndpointCheckResult(_validEndpoint.Id, Arg.Is<EndpointCheckResult>(r => r.Error.Message == "There is no registered handler for this kind of endpoint."), true);
        }

        [TestMethod]
        public void ShouldSendFailureIfCheckServiceThrows()
        {
            _goodCheckService.When(call => call.Check(Arg.Any<Endpoint>()))
                .Throw(new Exception("ERRORMSG"));
            _service.ProcessEndpoint(_validEndpoint, 0);

            _stateService.Received().AddEndpointCheckResult(_validEndpoint.Id, Arg.Is<EndpointCheckResult>(r => r.Error.Message == "Unexpected check failure: ERRORMSG"), true);
        }

        [TestMethod]
        public void ShouldRetryConfiguredNumberOfTimesBeforeFailing()
        {
            SetCheckServiceReturnValue(false, "ERRORMSG");
            _settingsService.Monitor_MaxRetries.Returns(4);

            _service.ProcessEndpoint(_validEndpoint, 0);

            _goodCheckService.Received(5).Check(Arg.Any<Endpoint>());

            _stateService.Received(4).AddEndpointCheckResult(_validEndpoint.Id, Arg.Is<EndpointCheckResult>(r => r.Success == false), false);
            _stateService.Received(1).AddEndpointCheckResult(_validEndpoint.Id, Arg.Is<EndpointCheckResult>(r => r.Success == false), true);
        }

        [TestMethod]
        public void ShouldNotProcessChangedEndpoint()
        {
            _validEndpoint.LastUpdated = DateTimeOffset.UtcNow.AddDays(2);
            _service.ProcessEndpoint(_validEndpoint, 0);

            _badCheckService1.DidNotReceive().CanHandle(Arg.Any<Endpoint>());
            _goodCheckService.DidNotReceive().CanHandle(Arg.Any<Endpoint>());
            _badCheckService2.DidNotReceive().CanHandle(Arg.Any<Endpoint>());
            _goodCheckService.DidNotReceive().Check(Arg.Any<Endpoint>());
        }

        [TestMethod]
        public void ShouldNotRespondToSuccessWhenEndpointChanged()
        {
            _goodCheckService.Check(Arg.Any<Endpoint>()).Returns(call =>
            {
                TouchEndpoint(_validEndpoint);

                return new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = true };
            });

            _service.ProcessEndpoint(_validEndpoint, 0);

            _goodCheckService.Received().Check(Arg.Any<Endpoint>());
            _stateService.DidNotReceive().GetStatus(Arg.Any<int>());
            _stateService.DidNotReceive().AddEndpointCheckResult(Arg.Any<int>(), Arg.Any<EndpointCheckResult>(), Arg.Any<bool>());
        }

        [TestMethod]
        public void ShouldNotRespondToFailureWhenEndpointChanged()
        {
            _goodCheckService.Check(Arg.Any<Endpoint>()).Returns(call =>
            {
                TouchEndpoint(_validEndpoint);

                return new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = false, Error = new Exception("ERRORMSG") };
            });

            _service.ProcessEndpoint(_validEndpoint, 0);

            _goodCheckService.Received().Check(Arg.Any<Endpoint>());
            _stateService.DidNotReceive().GetStatus(Arg.Any<int>());
            _stateService.DidNotReceive().AddEndpointCheckResult(Arg.Any<int>(), Arg.Any<EndpointCheckResult>(), Arg.Any<bool>());
        }

        private void TouchEndpoint(Endpoint endpoint)
        {
            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                var dbEndpoint = dbContext.Endpoints.First(e => e.Id == endpoint.Id);
                dbEndpoint.LastUpdated = dbEndpoint.LastUpdated.AddMinutes(1);
                dbContext.SaveChanges();
            }
        }

        private void SetCheckServiceReturnValue(bool success, string failureMessage)
        {
            _goodCheckService.Check(Arg.Any<Endpoint>()).Returns(call =>
            {
                if (success)
                    return new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = true };

                return new EndpointCheckResult { Start = DateTimeOffset.UtcNow, Stop = DateTimeOffset.UtcNow, Success = false, Error = new Exception(failureMessage) };
            });
        }
    }
}
