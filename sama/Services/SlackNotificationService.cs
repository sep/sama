using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _provider;

        public SlackNotificationService(ILogger<SlackNotificationService> logger, IConfigurationRoot config, IServiceProvider provider)
        {
            _logger = logger;
            _config = config;
            _provider = provider;
        }

        public void Notify(Endpoint endpoint, bool isUp, Exception exception)
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
            using (var client = new HttpClient(_provider.GetRequiredService<HttpMessageHandler>(), true))
            {
                var data = JsonConvert.SerializeObject(new { text = message });
                var url = _config.GetSection("SAMA").GetValue<string>("SlackWebHook");
                client.PostAsync(url, new StringContent(data)).Wait();
            }
        }
    }
}
