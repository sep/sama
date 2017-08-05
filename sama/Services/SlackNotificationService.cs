using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using sama.Models;
using System;
using System.Net.Http;

namespace sama.Services
{
    public class SlackNotificationService
    {
        private readonly ILogger<SlackNotificationService> _logger;
        private readonly IConfigurationRoot _config;
        private readonly HttpClientHandler _httpHandler;

        public SlackNotificationService(ILogger<SlackNotificationService> logger, IConfigurationRoot config, HttpClientHandler httpHandler)
        {
            _logger = logger;
            _config = config;
            _httpHandler = httpHandler;
        }

        public virtual void Notify(Endpoint endpoint, bool isUp, Exception exception)
        {
            if (isUp)
            {
                SendNotification($"The endpoint '{endpoint.Name}' is up. Hooray!");
            }
            else
            {
                SendNotification($"The endpoint '{endpoint.Name}' is down: {exception.Message}");
            }
        }

        private void SendNotification(string message)
        {
            try
            {
                using (var client = new HttpClient(_httpHandler, false))
                {
                    var data = JsonConvert.SerializeObject(new { text = message });
                    var url = _config.GetSection("SAMA").GetValue<string>("SlackWebHook");
                    client.PostAsync(url, new StringContent(data)).Wait();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, "Unable to send Slack notification", ex);
            }
        }
    }
}
