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
        private readonly SettingsService _settings;
        private readonly HttpClientHandler _httpHandler;

        public SlackNotificationService(ILogger<SlackNotificationService> logger, SettingsService settings, HttpClientHandler httpHandler)
        {
            _logger = logger;
            _settings = settings;
            _httpHandler = httpHandler;
        }

        public virtual void Notify(Endpoint endpoint, bool isUp, string failureMessage)
        {
            if (isUp)
            {
                SendNotification($"The endpoint '{endpoint.Name}' is up. Hooray!");
            }
            else
            {
                SendNotification($"The endpoint '{endpoint.Name}' is down: {failureMessage}");
            }
        }

        private void SendNotification(string message)
        {
            try
            {
                using (var client = new HttpClient(_httpHandler, false))
                {
                    var data = JsonConvert.SerializeObject(new { text = message });
                    var url = _settings.Notifications_Slack_WebHook;
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
