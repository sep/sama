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
        private SlackNotificationService _slackService;
        private ICheckService _goodCheckService;
        private ICheckService _badCheckService1;
        private ICheckService _badCheckService2;
        private Endpoint _validEndpoint;

        [TestInitialize]
        public void Setup()
        {
            _provider = TestUtility.InitDI();
            _settingsService = Substitute.For<SettingsService>((IServiceProvider)null);
            _stateService = Substitute.For<StateService>();
            _slackService = Substitute.For<SlackNotificationService>(null, null, null);
            _goodCheckService = Substitute.For<ICheckService>();
            _badCheckService1 = Substitute.For<ICheckService>();
            _badCheckService2 = Substitute.For<ICheckService>();

            _service = new EndpointProcessService(_settingsService, _stateService, _slackService, new List<ICheckService> { _badCheckService1, _goodCheckService, _badCheckService2 }, _provider);

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

            _goodCheckService.Received().Check(Arg.Any<Endpoint>(), out string _);
            _badCheckService1.DidNotReceive().Check(Arg.Any<Endpoint>(), out string _);
            _badCheckService2.DidNotReceive().Check(Arg.Any<Endpoint>(), out string _);
        }

        [TestMethod]
        public void ShouldSendFailureIfNoMatchingCheckServices()
        {
            _goodCheckService.CanHandle(Arg.Any<Endpoint>()).Returns(false);
            _service.ProcessEndpoint(_validEndpoint, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), false, "There is no registered handler for this kind of endpoint.");
        }

        [TestMethod]
        public void ShouldSendFailureIfCheckServiceThrows()
        {
            _goodCheckService.When(call => call.Check(Arg.Any<Endpoint>(), out string _))
                .Throw(new Exception("ERRORMSG"));
            _service.ProcessEndpoint(_validEndpoint, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), false, "Unexpected check failure: ERRORMSG");
        }

        [TestMethod]
        public void ShouldNotSendSuccessMessageIfFirstTimeSuccess()
        {
            SetCheckServiceReturnValue(true, null);
            _service.ProcessEndpoint(_validEndpoint, 0);

            _slackService.DidNotReceive().Notify(Arg.Any<Endpoint>(), Arg.Any<bool>(), Arg.Any<string>());
        }

        [TestMethod]
        public void ShouldNotSendSuccessMessageAfterPreviousSuccess()
        {
            SetCheckServiceReturnValue(true, null);
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = true });

            _service.ProcessEndpoint(_validEndpoint, 0);

            _slackService.DidNotReceive().Notify(Arg.Any<Endpoint>(), Arg.Any<bool>(), Arg.Any<string>());
        }

        [TestMethod]
        public void ShouldSendSuccessMessageAfterSettingsChange()
        {
            SetCheckServiceReturnValue(true, null);
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = null });

            _service.ProcessEndpoint(_validEndpoint, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), true, Arg.Any<string>());
        }

        [TestMethod]
        public void ShouldSendSuccessMessageAfterPreviousFailure()
        {
            SetCheckServiceReturnValue(true, null);
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = false });

            _service.ProcessEndpoint(_validEndpoint, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), true, Arg.Any<string>());
        }

        [TestMethod]
        public void ShouldRetryConfiguredNumberOfTimesBeforeFailing()
        {
            SetCheckServiceReturnValue(false, "ERRORMSG");
            _settingsService.Monitor_MaxRetries.Returns(4);

            _service.ProcessEndpoint(_validEndpoint, 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), false, "ERRORMSG");
            _goodCheckService.Received(5).Check(Arg.Any<Endpoint>(), out string _);
        }

        [TestMethod]
        public void ShouldNotProcessChangedEndpoint()
        {
            _validEndpoint.LastUpdated = DateTimeOffset.UtcNow.AddDays(2);
            _service.ProcessEndpoint(_validEndpoint, 0);

            _badCheckService1.DidNotReceive().CanHandle(Arg.Any<Endpoint>());
            _goodCheckService.DidNotReceive().CanHandle(Arg.Any<Endpoint>());
            _badCheckService2.DidNotReceive().CanHandle(Arg.Any<Endpoint>());
            _goodCheckService.DidNotReceive().Check(Arg.Any<Endpoint>(), out string _);
        }

        [TestMethod]
        public void ShouldNotRespondToSuccessWhenEndpointChanged()
        {
            _goodCheckService.Check(Arg.Any<Endpoint>(), out string msg).Returns(call =>
            {
                TouchEndpoint(_validEndpoint);

                msg = null;
                return true;
            });

            _service.ProcessEndpoint(_validEndpoint, 0);

            _goodCheckService.Received().Check(Arg.Any<Endpoint>(), out string _);
            _stateService.DidNotReceive().GetState(Arg.Any<int>());
            _stateService.DidNotReceive().SetState(Arg.Any<Endpoint>(), Arg.Any<bool?>(), Arg.Any<string>());
            _slackService.DidNotReceive().Notify(Arg.Any<Endpoint>(), Arg.Any<bool>(), Arg.Any<string>());
        }

        [TestMethod]
        public void ShouldNotRespondToFailureWhenEndpointChanged()
        {
            _goodCheckService.Check(Arg.Any<Endpoint>(), out string msg).Returns(call =>
            {
                TouchEndpoint(_validEndpoint);

                msg = "ERRORMSG";
                return false;
            });

            _service.ProcessEndpoint(_validEndpoint, 0);

            _goodCheckService.Received().Check(Arg.Any<Endpoint>(), out string _);
            _stateService.DidNotReceive().GetState(Arg.Any<int>());
            _stateService.DidNotReceive().SetState(Arg.Any<Endpoint>(), Arg.Any<bool?>(), Arg.Any<string>());
            _slackService.DidNotReceive().Notify(Arg.Any<Endpoint>(), Arg.Any<bool>(), Arg.Any<string>());
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
            _goodCheckService.Check(Arg.Any<Endpoint>(), out string msg).Returns(call =>
            {
                call[1] = failureMessage;
                return success;
            });
        }
    }
}
