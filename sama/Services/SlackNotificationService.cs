using Humanizer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using sama.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace sama.Services
{
    public class SlackNotificationService : INotificationService
    {
        private static int NotifyUpQueueDelaySeconds = 2;

        private readonly ILogger<SlackNotificationService> _logger;
        private readonly SettingsService _settings;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentBag<Endpoint> _delayNotifyUpEndpoints;
        private readonly Timer _delayNotifyUpTimer;

        public SlackNotificationService(ILogger<SlackNotificationService> logger, SettingsService settings, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _settings = settings;
            _serviceProvider = serviceProvider;

            _delayNotifyUpEndpoints = new ConcurrentBag<Endpoint>();
            _delayNotifyUpTimer = new Timer(_ => SendDelayedNotification());
        }

        public void NotifyMisc(Endpoint endpoint, NotificationType type)
        {
            switch (type)
            {
                case NotificationType.EndpointAdded:
                    SendNotification($"The endpoint {FormatEndpointName(endpoint.Name)} has been added and will be checked shortly.");
                    break;
                case NotificationType.EndpointRemoved:
                    SendNotification($"The endpoint {FormatEndpointName(endpoint.Name)} has been removed.");
                    break;
                case NotificationType.EndpointEnabled:
                    SendNotification($"The endpoint {FormatEndpointName(endpoint.Name)} has been enabled and will be checked shortly.");
                    break;
                case NotificationType.EndpointDisabled:
                    SendNotification($"The endpoint {FormatEndpointName(endpoint.Name)} has been disabled.");
                    break;
                case NotificationType.EndpointReconfigured:
                    SendNotification($"The endpoint {FormatEndpointName(endpoint.Name)} has been reconfigured and will be checked shortly.");
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

                if (reason is SslException sslEx){
                    failureMessage += "\n Details: ```\n" + sslEx.Details + "```";
                }
            }

            SendNotification($"The endpoint {FormatEndpointName(endpoint.Name)} is down: {failureMessage}");
        }

        public void NotifyUp(Endpoint endpoint, DateTimeOffset? downAsOf)
        {
            if (downAsOf.HasValue)
            {
                var downLength = DateTimeOffset.UtcNow - downAsOf.Value;
                SendNotification($"The endpoint {FormatEndpointName(endpoint.Name)} is up after being down for {downLength.Humanize()}. Hooray!");
            }
            else
            {
                //SendNotification($"The endpoint '{endpoint.Name}' is up. Hooray!");
                EnqueueDelayedUpNotification(endpoint);
            }
        }

        private void SendNotification(string message)
        {
            var url = _settings.Notifications_Slack_WebHook;
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                using (var httpHandler = _serviceProvider.GetRequiredService<HttpClientHandler>())
                using (var client = new HttpClient(httpHandler, false))
                {
                    var data = JsonConvert.SerializeObject(new { text = message });
                    client.PostAsync(url, new StringContent(data)).Wait();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(0, "Unable to send Slack notification", ex);
            }
        }

        private void EnqueueDelayedUpNotification(Endpoint endpoint)
        {
            _delayNotifyUpEndpoints.Add(endpoint);
            _delayNotifyUpTimer.Change(TimeSpan.FromSeconds(NotifyUpQueueDelaySeconds), Timeout.InfiniteTimeSpan);
        }

        private void SendDelayedNotification()
        {
            var endpoints = new List<Endpoint>();
            while (_delayNotifyUpEndpoints.TryTake(out var endpoint))
            {
                endpoints.Add(endpoint);
            }

            if (endpoints.Count < 1)
            {
                return;
            }
            else if (endpoints.Count == 1)
            {
                SendNotification($"The endpoint {FormatEndpointName(endpoints.First().Name)} is up. Hooray!");
            }
            else
            {
                var stringifiedEndpoints = string.Join(", ", endpoints.Select(ep => FormatEndpointName(ep.Name)));
                SendNotification($"The following endpoints are up: {stringifiedEndpoints}. Hooray!");
            }
        }

        private string FormatEndpointName(string rawName) => $"`{rawName.Replace("`", "")}`";
    }
}
