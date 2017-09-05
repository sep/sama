using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using sama.Models;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using sama.Extensions;

namespace sama.Services
{
    public class HttpCheckService : ICheckService
    {
        private readonly SettingsService _settingsService;
        private readonly IServiceProvider _serviceProvider;

        public HttpCheckService(SettingsService settingsService, IServiceProvider serviceProvider)
        {
            _settingsService = settingsService;
            _serviceProvider = serviceProvider;
        }

        public bool CanHandle(Endpoint endpoint)
        {
            return (endpoint.Kind == Endpoint.EndpointKind.Http);
        }

        public bool Check(Endpoint endpoint, out string failureMessage)
        {
            using (var httpHandler = _serviceProvider.GetRequiredService<HttpClientHandler>())
            using (var client = new HttpClient(httpHandler, false))
            using (var message = new HttpRequestMessage(HttpMethod.Get, endpoint.GetHttpLocation()))
            {
                var statusCodes = endpoint.GetHttpStatusCodes() ?? new List<int>();
                if (statusCodes.Count > 0)
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
                    if (ex is AggregateException)
                        ex = ex.InnerException;
                    if (ex is HttpRequestException && ex.InnerException != null)
                        ex = ex.InnerException;
                    if (ex is TaskCanceledException)
                        ex = new Exception($"The request timed out after {ClientTimeout.TotalSeconds} sec.");

                    failureMessage = ex.Message;
                    return false;
                }

                var response = task.Result;
                if (!IsExpectedStatusCode(endpoint, response))
                {
                    failureMessage = $"HTTP status code is {(int)response.StatusCode}.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(endpoint.GetHttpResponseMatch()))
                {
                    failureMessage = null;
                    return true;
                }

                var contentTask = response.Content.ReadAsStringAsync();
                try
                {
                    contentTask.Wait();
                }
                catch (Exception ex)
                {
                    failureMessage = $"Failed to read HTTP content: {ex.Message}.";
                    return false;
                }

                var index = contentTask.Result.IndexOf(endpoint.GetHttpResponseMatch());
                if (index < 0)
                {
                    failureMessage = "The keyword match was not found.";
                    return false;
                }
                else
                {
                    failureMessage = null;
                    return true;
                }
            }
        }

        private TimeSpan ClientTimeout
        {
            get
            {
                var seconds = _settingsService.Monitor_RequestTimeoutSeconds;
                return TimeSpan.FromSeconds(seconds);
            }
        }

        private bool IsExpectedStatusCode(Endpoint endpoint, HttpResponseMessage response)
        {
            var statusCodes = endpoint.GetHttpStatusCodes() ?? new List<int>();
            if (statusCodes.Count < 1)
            {
                return response.IsSuccessStatusCode;
            }
            else
            {
                var statusCode = (int)response.StatusCode;
                return statusCodes.Contains(statusCode);
            }
        }
    }
}
