using Humanizer;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using sama.Models;
using System;
using System.Net.Http;

namespace sama.Services
{
    public class SlackNotificationService : INotificationService
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

        public void NotifySingleResult(Endpoint endpoint, EndpointCheckResult result)
        {
            // Ignore this notification type.
        }

        public void NotifyDown(Endpoint endpoint, DateTimeOffset downAsOf, Exception reason)
        {
            var failureMessage = reason?.Message;

            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                var msg = failureMessage.Trim();
                if (!msg.EndsWith('.') && !msg.EndsWith('!') && !msg.EndsWith('?'))
                    failureMessage = failureMessage.Trim() + '.';
            }

            SendNotification($"The endpoint '{endpoint.Name}' is down: {failureMessage}");
        }

        public void NotifyUp(Endpoint endpoint, DateTimeOffset? downAsOf)
        {
            if (downAsOf.HasValue)
            {
                var downLength = DateTimeOffset.UtcNow - downAsOf.Value;
                SendNotification($"The endpoint '{endpoint.Name}' is up after being down for {downLength.Humanize()}. Hooray!");
            }
            else
            {
                SendNotification($"The endpoint '{endpoint.Name}' is up. Hooray!");
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
