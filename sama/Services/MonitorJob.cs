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
        private readonly Dictionary<int, string> _downEndpoints = new Dictionary<int, string>();
        private readonly IConfigurationRoot _config;

        public MonitorJob(DbContextOptions<ApplicationDbContext> db, IConfigurationRoot config)
        {
            _dbContextOptions = db;
            _config = config;
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
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                var task = client.GetAsync(endpoint.Location);
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

        private void SetEndpointSuccess(Endpoint endpoint)
        {
            if (_downEndpoints.ContainsKey(endpoint.Id))
            {
                // It's back up!
                _downEndpoints.Remove(endpoint.Id);
                SendNotification($"The endpoint '{endpoint.Name}' is back up. Hooray!");
            }
        }

        private void SetEndpointFailure(Endpoint endpoint, Exception exception)
        {
            if (exception is AggregateException)
                exception = exception.InnerException;
            if (exception is HttpRequestException && exception.InnerException != null)
                exception = exception.InnerException;

            var message = exception.Message;

            if (!_downEndpoints.ContainsKey(endpoint.Id))
            {
                // It's down!
                _downEndpoints.Add(endpoint.Id, message);
                SendNotification($"The endpoint '{endpoint.Name}' is down: {message}");
            }
            else if (_downEndpoints[endpoint.Id] != message)
            {
                // Something different has gone wrong.
                _downEndpoints[endpoint.Id] = message;
                SendNotification($"The endpoint '{endpoint.Name}' is down: {message}");
            }
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
