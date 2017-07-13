using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using sama.Models;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace sama.Services
{
    public class EndpointCheckService
    {
        private readonly IConfigurationRoot _config;
        private readonly StateService _stateService;

        public EndpointCheckService(IConfigurationRoot config, StateService stateService)
        {
            _config = config;
            _stateService = stateService;
        }

        public void ProcessEndpoint(Endpoint endpoint, int retryCount)
        {
            using (var handler = new HttpClientHandler())
            {
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    // Ignore SSL/TLS errors (for now)
                    return true;
                };

                using (var client = new HttpClient(handler, false))
                using (var message = new HttpRequestMessage(HttpMethod.Get, endpoint.Location))
                {
                    message.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko");
                    message.Headers.Add("Accept", "text/html");
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
                    if (!response.IsSuccessStatusCode)
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
        }

        private void SetEndpointSuccess(Endpoint endpoint)
        {
            var previous = _stateService.GetState(endpoint.Id);
            if (previous != null && previous.IsUp != true)
            {
                // It's back up!
                SendNotification($"The endpoint '{endpoint.Name}' is up. Hooray!");
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

            var message = exception.Message;

            var previous = _stateService.GetState(endpoint.Id);
            if (previous?.IsUp == null || previous?.IsUp == true)
            {
                // It's down!
                SendNotification($"The endpoint '{endpoint.Name}' is down: {message}");
            }
            else if (previous?.Exception?.Message != message)
            {
                // Something different has gone wrong.
                SendNotification($"The endpoint '{endpoint.Name}' is down: {message}");
            }
            _stateService.SetState(endpoint, false, exception);
        }

        private void SendNotification(string message)
        {
            using (var client = new HttpClient())
            {
                var data = JsonConvert.SerializeObject(new { text = message });
                var url = _config.GetSection("SAMA").GetValue<string>("SlackWebHook");
                client.PostAsync(url, new StringContent(data)).Wait();
            }
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
                return TimeSpan.FromSeconds(1);
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
    }
}
