using Humanizer;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _serviceProvider;

        public SlackNotificationService(ILogger<SlackNotificationService> logger, SettingsService settings, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _settings = settings;
            _serviceProvider = serviceProvider;
        }

        public void NotifyMisc(Endpoint endpoint, NotificationType type)
        {
            switch (type)
            {
                case NotificationType.EndpointAdded:
                    SendNotification($"The endpoint '{endpoint.Name}' has been added.");
                    break;
                case NotificationType.EndpointRemoved:
                    SendNotification($"The endpoint '{endpoint.Name}' has been removed.");
                    break;
                case NotificationType.EndpointEnabled:
                    SendNotification($"The endpoint '{endpoint.Name}' has been enabled.");
                    break;
                case NotificationType.EndpointDisabled:
                    SendNotification($"The endpoint '{endpoint.Name}' has been disabled.");
                    break;
                case NotificationType.EndpointReconfigured:
                    SendNotification($"The endpoint '{endpoint.Name}' has been reconfigured.");
                    break;
                default:
                    return;
            }
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
            var url = _settings.Notifications_Slack_WebHook;
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                using (var httpHandler = _serviceProvider.GetRequiredService<HttpClientHandler>())
                {
                    httpHandler.ServerCertificateCustomValidationCallback = null;
                    using (var client = new HttpClient(httpHandler, false))
                    {
                        var data = JsonConvert.SerializeObject(new { text = message });
                        client.PostAsync(url, new StringContent(data)).Wait();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, "Unable to send Slack notification", ex);
            }
        }
    }
}
