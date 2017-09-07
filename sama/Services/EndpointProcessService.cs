using sama.Models;
using System;
using System.Threading;
using System.Collections.Generic;

namespace sama.Services
{
    public class EndpointProcessService
    {
        private readonly SettingsService _settingsService;
        private readonly StateService _stateService;
        private readonly SlackNotificationService _notifyService;
        private readonly IEnumerable<ICheckService> _checkServices;

        public EndpointProcessService(SettingsService settingsService, StateService stateService, SlackNotificationService notifyService, IEnumerable<ICheckService> checkServices)
        {
            _settingsService = settingsService;
            _stateService = stateService;
            _notifyService = notifyService;
            _checkServices = checkServices;
        }

        public virtual void ProcessEndpoint(Endpoint endpoint, int retryCount)
        {
            var service = GetCheckService(endpoint);

            if (service == null)
            {
                SetEndpointFailure(endpoint, "There is no registered handler for this kind of endpoint.", retryCount);
                return;
            }

            if (!service.Check(endpoint, out string failureMessage))
            {
                SetEndpointFailure(endpoint, failureMessage, retryCount);
            }
            else
            {
                SetEndpointSuccess(endpoint);
            }
        }

        private ICheckService GetCheckService(Endpoint endpoint)
        {
            foreach (var service in _checkServices)
            {
                if (service.CanHandle(endpoint)) return service;
            }

            return null;
        }

        private void SetEndpointSuccess(Endpoint endpoint)
        {
            var previous = _stateService.GetState(endpoint.Id);
            if (previous != null && previous.IsUp != true)
            {
                // It's back up!
                _notifyService.Notify(endpoint, true, null);
            }
            _stateService.SetState(endpoint, true, null);
        }

        private void SetEndpointFailure(Endpoint endpoint, string failureMessage, int retryCount)
        {
            if (retryCount < MaxRetries)
            {
                Thread.Sleep(RetrySleep);
                ProcessEndpoint(endpoint, retryCount + 1);
                return;
            }

            var previous = _stateService.GetState(endpoint.Id);
            if (previous?.IsUp == null || previous?.IsUp == true || previous?.FailureMessage != failureMessage)
            {
                // It's down!
                _notifyService.Notify(endpoint, false, failureMessage);
            }

            _stateService.SetState(endpoint, false, failureMessage);
        }

        private int MaxRetries
        {
            get
            {
                // 1 retry == 2 total tries
                return _settingsService.Monitor_MaxRetries;
            }
        }

        private TimeSpan RetrySleep
        {
            get
            {
                var seconds = _settingsService.Monitor_SecondsBetweenTries;
                return TimeSpan.FromSeconds(seconds);
            }
        }
    }
}
