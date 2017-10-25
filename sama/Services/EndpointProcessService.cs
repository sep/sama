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
        private readonly IEnumerable<ICheckService> _checkServices;
        private readonly IServiceProvider _provider;

        public EndpointProcessService(SettingsService settingsService, StateService stateService, IEnumerable<ICheckService> checkServices, IServiceProvider provider)
        {
            _settingsService = settingsService;
            _stateService = stateService;
            _checkServices = checkServices;
            _provider = provider;
        }

        public virtual void ProcessEndpoint(Endpoint endpoint, int retryCount)
        {
            if (!IsEndpointCurrent(endpoint)) return;

            var service = GetCheckService(endpoint);

            if (service == null)
            {
                var result = new EndpointCheckResult {
                    Start = DateTimeOffset.UtcNow,
                    Stop = DateTimeOffset.UtcNow,
                    Success = false,
                    Error = new Exception("There is no registered handler for this kind of endpoint.")
                };
                SetEndpointFailure(endpoint, result, retryCount);
                return;
            }

            try
            {
                var result = service.Check(endpoint);
                if (result.Success)
                {
                    SetEndpointSuccess(endpoint, result);
                }
                else
                {
                    SetEndpointFailure(endpoint, result, retryCount);
                }
            }
            catch (Exception ex)
            {
                var result = new EndpointCheckResult
                {
                    Start = DateTimeOffset.UtcNow,
                    Stop = DateTimeOffset.UtcNow,
                    Success = false,
                    Error = new Exception($"Unexpected check failure: {ex.Message}", ex)
                };
                SetEndpointFailure(endpoint, result, retryCount);
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

        private void SetEndpointSuccess(Endpoint endpoint, EndpointCheckResult result)
        {
            if (!IsEndpointCurrent(endpoint)) return;

            _stateService.AddEndpointCheckResult(endpoint.Id, result, true);
        }

        private void SetEndpointFailure(Endpoint endpoint, EndpointCheckResult result, int retryCount)
        {
            if (!IsEndpointCurrent(endpoint)) return;

            if (retryCount < MaxRetries)
            {
                _stateService.AddEndpointCheckResult(endpoint.Id, result, false);

                Thread.Sleep(RetrySleep);
                ProcessEndpoint(endpoint, retryCount + 1);
                return;
            }

            _stateService.AddEndpointCheckResult(endpoint.Id, result, true);
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
