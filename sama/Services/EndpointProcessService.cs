using sama.Models;
using System;
using System.Threading;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace sama.Services
{
    public class EndpointProcessService
    {
        private readonly SettingsService _settingsService;
        private readonly StateService _stateService;
        private readonly SlackNotificationService _notifyService;
        private readonly IEnumerable<ICheckService> _checkServices;
        private readonly IServiceProvider _provider;

        public EndpointProcessService(SettingsService settingsService, StateService stateService, SlackNotificationService notifyService, IEnumerable<ICheckService> checkServices, IServiceProvider provider)
        {
            _settingsService = settingsService;
            _stateService = stateService;
            _notifyService = notifyService;
            _checkServices = checkServices;
            _provider = provider;
        }

        public virtual void ProcessEndpoint(Endpoint endpoint, int retryCount)
        {
            if (!IsEndpointCurrent(endpoint)) return;

            var service = GetCheckService(endpoint);

            if (service == null)
            {
                SetEndpointFailure(endpoint, "There is no registered handler for this kind of endpoint.", retryCount);
                return;
            }

            try
            {
                if (service.Check(endpoint, out string failureMessage))
                {
                    SetEndpointSuccess(endpoint);
                }
                else
                {
                    SetEndpointFailure(endpoint, failureMessage, retryCount);
                }
            }
            catch (Exception ex)
            {
                SetEndpointFailure(endpoint, $"Unexpected check failure: {ex.Message}", retryCount);
                return;
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
            if (!IsEndpointCurrent(endpoint)) return;

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
            if (!IsEndpointCurrent(endpoint)) return;

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

        private bool IsEndpointCurrent(Endpoint endpoint)
        {
            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                var latest = dbContext.Endpoints.FirstOrDefault(e => e.Id == endpoint.Id);
                return latest?.LastUpdated == endpoint.LastUpdated;
            }
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
