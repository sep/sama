using Microsoft.Extensions.Configuration;
using sama.Models;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace sama.Services
{
    public class EndpointCheckService
    {
        private readonly IConfigurationRoot _config;
        private readonly StateService _stateService;
        private readonly SlackNotificationService _notifyService;
        private readonly IServiceProvider _serviceProvider;

        public EndpointCheckService(IConfigurationRoot config, StateService stateService, SlackNotificationService notifyService, IServiceProvider serviceProvider)
        {
            _config = config;
            _stateService = stateService;
            _notifyService = notifyService;
            _serviceProvider = serviceProvider;
        }

        public virtual void ProcessEndpoint(Endpoint endpoint, int retryCount)
        {
            using (var httpHandler = _serviceProvider.GetRequiredService<HttpClientHandler>())
            using (var client = new HttpClient(httpHandler, false))
            using (var message = new HttpRequestMessage(HttpMethod.Get, endpoint.Location))
            {
                if (!string.IsNullOrWhiteSpace(endpoint.StatusCodes))
                    httpHandler.AllowAutoRedirect = false;

                message.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko");
                message.Headers.Add("Accept", "text/html, application/xhtml+xml, */*");
                client.Timeout = ClientTimeout;

                var task = client.SendAsync(message);
                try
                {
                    task.Wait();
                }
                catch (Exception ex)
                {
                    SetEndpointFailure(endpoint, ex, retryCount);
                    return;
                }

                var response = task.Result;
                if (!IsExpectedStatusCode(endpoint, response))
                {
                    SetEndpointFailure(endpoint, new Exception($"HTTP status code is {(int)response.StatusCode}."), retryCount);
                    return;
                }

                if (string.IsNullOrWhiteSpace(endpoint.ResponseMatch))
                {
                    SetEndpointSuccess(endpoint);
                    return;
                }

                var contentTask = response.Content.ReadAsStringAsync();
                try
                {
                    contentTask.Wait();
                }
                catch (Exception ex)
                {
                    SetEndpointFailure(endpoint, new Exception("Failed to read HTTP content.", ex), retryCount);
                    return;
                }

                var index = contentTask.Result.IndexOf(endpoint.ResponseMatch);
                if (index < 0)
                {
                    SetEndpointFailure(endpoint, new Exception("The keyword match was not found."), retryCount);
                }
                else
                {
                    SetEndpointSuccess(endpoint);
                }
            }
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

        private void SetEndpointFailure(Endpoint endpoint, Exception exception, int retryCount)
        {
            if (retryCount < MaxRetries)
            {
                Thread.Sleep(RetrySleep);
                ProcessEndpoint(endpoint, retryCount + 1);
                return;
            }

            // Massage the exception into something more useful.
            if (exception is AggregateException)
                exception = exception.InnerException;
            if (exception is HttpRequestException && exception.InnerException != null)
                exception = exception.InnerException;
            if (exception is TaskCanceledException)
                exception = new Exception($"The request timed out after {ClientTimeout.TotalSeconds} sec.");

            var previous = _stateService.GetState(endpoint.Id);
            if (previous?.IsUp == null || previous?.IsUp == true || previous?.Exception?.Message != exception.Message)
            {
                // It's down!
                _notifyService.Notify(endpoint, false, exception);
            }

            _stateService.SetState(endpoint, false, exception);
        }

        private int MaxRetries
        {
            get
            {
                // 1 retry == 2 total tries
                return _config.GetSection("SAMA").GetValue("MaxRetryCount", 1);
            }
        }

        private TimeSpan RetrySleep
        {
            get
            {
                var seconds = _config.GetSection("SAMA").GetValue("SecondsBetweenTries", 1);
                return TimeSpan.FromSeconds(seconds);
            }
        }

        private TimeSpan ClientTimeout
        {
            get
            {
                var seconds = _config.GetSection("SAMA").GetValue("HttpRequestTimeoutSeconds", 15);
                return TimeSpan.FromSeconds(seconds);
            }
        }

        private bool IsExpectedStatusCode(Endpoint endpoint, HttpResponseMessage response)
        {
            if (string.IsNullOrEmpty(endpoint.StatusCodes))
            {
                return response.IsSuccessStatusCode;
            }
            else
            {
                var statusCodes = endpoint.StatusCodes.Replace(" ", "").Split(',').ToList();
                var statusCode = (int)response.StatusCode;
                return statusCodes.Contains(statusCode.ToString());
            }
        }
    }
}
