using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;

namespace TestSama.Services
{
    [TestClass]
    public class EndpointCheckServiceTests
    {
        private EndpointCheckService _service;
        private SettingsService _settingsService;
        private StateService _stateService;
        private SlackNotificationService _slackService;
        private ICheckService _goodCheckService;
        private ICheckService _badCheckService1;
        private ICheckService _badCheckService2;

        [TestInitialize]
        public void Setup()
        {
            _stateService = Substitute.For<StateService>();
            _settingsService = Substitute.For<SettingsService>((IServiceProvider)null);
            _slackService = Substitute.For<SlackNotificationService>(null, null, null);
            _goodCheckService = Substitute.For<ICheckService>();
            _badCheckService1 = Substitute.For<ICheckService>();
            _badCheckService2 = Substitute.For<ICheckService>();

            _service = new EndpointCheckService(_settingsService, _stateService, _slackService, new List<ICheckService> { _badCheckService1, _goodCheckService, _badCheckService2 });

            _goodCheckService.CanHandle(Arg.Any<Endpoint>()).Returns(true);
            _badCheckService1.CanHandle(Arg.Any<Endpoint>()).Returns(false);
            _badCheckService2.CanHandle(Arg.Any<Endpoint>()).Returns(false);
        }

        [TestMethod]
        public void ShouldSelectFirstMatchingCheckService()
        {
            _service.ProcessEndpoint(new Endpoint(), 0);

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
            _service.ProcessEndpoint(TestUtility.CreateHttpEndpoint("A"), 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), false, "There is no registered handler for this kind of endpoint.");
        }

        [TestMethod]
        public void ShouldNotSendSuccessMessageIfFirstTimeSuccess()
        {
            SetCheckServiceReturnValue(true, null);
            _service.ProcessEndpoint(TestUtility.CreateHttpEndpoint("A"), 0);

            _slackService.DidNotReceive().Notify(Arg.Any<Endpoint>(), Arg.Any<bool>(), Arg.Any<string>());
        }

        [TestMethod]
        public void ShouldNotSendSuccessMessageAfterPreviousSuccess()
        {
            SetCheckServiceReturnValue(true, null);
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = true });

            _service.ProcessEndpoint(TestUtility.CreateHttpEndpoint("A"), 0);

            _slackService.DidNotReceive().Notify(Arg.Any<Endpoint>(), Arg.Any<bool>(), Arg.Any<string>());
        }

        [TestMethod]
        public void ShouldSendSuccessMessageAfterSettingsChange()
        {
            SetCheckServiceReturnValue(true, null);
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = null });

            _service.ProcessEndpoint(TestUtility.CreateHttpEndpoint("A"), 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), true, Arg.Any<string>());
        }

        [TestMethod]
        public void ShouldSendSuccessMessageAfterPreviousFailure()
        {
            SetCheckServiceReturnValue(true, null);
            _stateService.GetState(Arg.Any<int>()).Returns(new StateService.EndpointState { IsUp = false });

            _service.ProcessEndpoint(TestUtility.CreateHttpEndpoint("A"), 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), true, Arg.Any<string>());
        }

        [TestMethod]
        public void ShouldRetryConfiguredNumberOfTimesBeforeFailing()
        {
            SetCheckServiceReturnValue(false, "ERRORMSG");
            _settingsService.Monitor_MaxRetries.Returns(4);

            _service.ProcessEndpoint(TestUtility.CreateHttpEndpoint("A"), 0);

            _slackService.Received().Notify(Arg.Any<Endpoint>(), false, "ERRORMSG");
            _goodCheckService.Received(5).Check(Arg.Any<Endpoint>(), out string _);
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
