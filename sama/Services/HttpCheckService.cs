using Microsoft.Extensions.Configuration;
using sama.Extensions;
using sama.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Threading.Tasks;

namespace sama.Services
{
    public class HttpCheckService(SettingsService _settingsService, CertificateValidationService _certService, IConfiguration _configuration, HttpHandlerFactory _httpHandlerFactory) : ICheckService
    {
        private Version? _defaultRequestVersion;
        private HttpVersionPolicy? _defaultVersionPolicy;

        public bool CanHandle(Endpoint endpoint)
        {
            return (endpoint.Kind == Endpoint.EndpointKind.Http);
        }

        public EndpointCheckResult Check(Endpoint endpoint)
        {
            var result = new EndpointCheckResult { Start = DateTimeOffset.UtcNow };

            var statusCodes = endpoint.GetHttpStatusCodes() ?? [];
            var sslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    _certService.ValidateHttpEndpoint(endpoint, chain, sslPolicyErrors);
                    return true;
                }
            };

            using var httpHandler = _httpHandlerFactory.Create((statusCodes.Count == 0), sslOptions);
            using var client = new HttpClient(httpHandler, false);
            using var message = new HttpRequestMessage(HttpMethod.Get, endpoint.GetHttpLocation());

            message.Version = GetDefaultRequestVersion();
            message.VersionPolicy = GetDefaultVersionPolicy();

            message.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:109.0) Gecko/20100101 SAMA/1.0");
            message.Headers.Add("Accept", "text/html, application/xhtml+xml, */*");
            client.Timeout = ClientTimeout;

            var task = client.SendAsync(message);
            try
            {
                task.Wait();
            }
            catch (Exception ex)
            {
                if (ex is AggregateException && ex.InnerException != null)
                    ex = ex.InnerException;
                if (ex is HttpRequestException && ex.InnerException != null)
                    ex = ex.InnerException;
                if (ex is TaskCanceledException)
                    ex = new Exception($"The request timed out after {ClientTimeout.TotalSeconds} sec");

                SetFailure(result, ex);
                return result;
            }

            var response = task.Result;
            if (!IsExpectedStatusCode(endpoint, response))
            {
                SetFailure(result, new Exception($"HTTP status code is {(int)response.StatusCode}"));
                return result;
            }

            var responseMatch = endpoint.GetHttpResponseMatch();
            if (string.IsNullOrWhiteSpace(responseMatch))
            {
                SetSuccess(result);
                return result;
            }

            var contentTask = response.Content.ReadAsStringAsync();
            try
            {
                contentTask.Wait();
            }
            catch (Exception ex)
            {
                SetFailure(result, new Exception($"Failed to read HTTP content: {ex.Message}", ex));
                return result;
            }

            var index = contentTask.Result.IndexOf(responseMatch);
            if (index < 0)
            {
                SetFailure(result, new Exception("The keyword match was not found"));
                return result;
            }
            else
            {
                SetSuccess(result);
                return result;
            }
        }

        private Version GetDefaultRequestVersion()
        {
            _defaultRequestVersion ??= Utility.GetConfiguredHttpRequestVersion(_configuration["DefaultHttpVersion"]);
            return _defaultRequestVersion;
        }

        private HttpVersionPolicy GetDefaultVersionPolicy()
        {
            _defaultVersionPolicy ??= Utility.GetConfiguredHttpVersionPolicy(_configuration["DefaultHttpVersion"]);
            return _defaultVersionPolicy.Value;
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

        private void SetFailure(EndpointCheckResult result, Exception ex)
        {
            result.Error = ex;
            result.Success = false;
            result.Stop = DateTimeOffset.UtcNow;
            result.ResponseTime = null;
        }

        private void SetSuccess(EndpointCheckResult result)
        {
            result.Error = null;
            result.Success = true;
            result.Stop = DateTimeOffset.UtcNow;
            result.ResponseTime = result.Stop - result.Start;
        }
    }
}
