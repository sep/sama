using FluentScheduler;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using sama.Models;
using Newtonsoft.Json;
using Microsoft.Extensions.Configuration;

namespace sama.Services
{
    public class MonitorJob : IJob
    {
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
        private readonly IConfigurationRoot _config;
        private readonly StateService _stateService;

        public MonitorJob(DbContextOptions<ApplicationDbContext> db, IConfigurationRoot config, StateService stateService)
        {
            _dbContextOptions = db;
            _config = config;
            _stateService = stateService;
        }

        public void Execute()
        {
            using (var dbContext = new ApplicationDbContext(_dbContextOptions))
            {
                var endpoints = dbContext.Endpoints.Where(e => e.Enabled).ToList();
                Parallel.ForEach(endpoints, ProcessEndpoint);
            }
        }

        private void ProcessEndpoint(Models.Endpoint endpoint)
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
                    client.Timeout = TimeSpan.FromSeconds(10);

                    var task = client.SendAsync(message);
                    try
                    {
                        task.Wait();
                    }
                    catch (Exception ex)
                    {
                        SetEndpointFailure(endpoint, ex);
                        return;
                    }

                    var response = task.Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        SetEndpointFailure(endpoint, new Exception("HTTP status code is " + (int)response.StatusCode));
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
                        SetEndpointFailure(endpoint, new Exception("Failed to read HTTP content", ex));
                        return;
                    }

                    var index = contentTask.Result.IndexOf(endpoint.ResponseMatch);
                    if (index < 0)
                    {
                        SetEndpointFailure(endpoint, new Exception("The keyword match was not found"));
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

        private void SetEndpointFailure(Endpoint endpoint, Exception exception)
        {
            if (exception is AggregateException)
                exception = exception.InnerException;
            if (exception is HttpRequestException && exception.InnerException != null)
                exception = exception.InnerException;

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
    }
}
