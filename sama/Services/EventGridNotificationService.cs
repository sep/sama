using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Logging;
using sama.Models;
using System;
using System.Threading.Tasks;

namespace sama.Services;

public class EventGridNotificationService : INotificationService
{
    private readonly ILogger<EventGridNotificationService> _logger;
    private readonly SettingsService _settings;
    private readonly BackgroundExecutionWrapper _bgExec;
    private readonly EventGridPublisherClientWrapper _eventGridWrapper;

    public EventGridNotificationService(ILogger<EventGridNotificationService> logger, SettingsService settings, BackgroundExecutionWrapper bgExec, EventGridPublisherClientWrapper eventGridWrapper)
    {
        _logger = logger;
        _settings = settings;
        _bgExec = bgExec;
        _eventGridWrapper = eventGridWrapper;
    }

    private static class EventTypes
    {
        public const string CheckCompleted = "sama.endpoint.check.completed";
        public const string StatusUp = "sama.endpoint.status.up";
        public const string StatusDown = "sama.endpoint.status.down";
        public const string ManagementAdded = "sama.endpoint.management.added";
        public const string ManagementRemoved = "sama.endpoint.management.removed";
        public const string ManagementEnabled = "sama.endpoint.management.enabled";
        public const string ManagementDisabled = "sama.endpoint.management.disabled";
        public const string ManagementReconfigured = "sama.endpoint.management.reconfigured";
        public const string ManagementUnknown = "sama.endpoint.management.unknown";
    }

private static string FormatSubject(string path) => $"sama/{path}";
private static string FormatEndpointSubject(string path) => FormatSubject($"endpoints/{path}");

    public virtual void NotifySingleResult(Endpoint endpoint, EndpointCheckResult result)
    {
        SendEvent(
            EventTypes.CheckCompleted,
            FormatEndpointSubject($"{endpoint.Id}"),
            new
            {
                endpointId = endpoint.Id,
                endpointName = endpoint.Name,
                success = result.Success,
                responseTime = result.ResponseTime?.TotalMilliseconds,
                startTime = result.Start,
                stopTime = result.Stop,
                error = result.Error?.Message
            }
        );
    }

    public virtual void NotifyUp(Endpoint endpoint, DateTimeOffset? downAsOf)
    {
        var downtimeMinutes = downAsOf.HasValue
            ? (int)DateTimeOffset.UtcNow.Subtract(downAsOf.Value).TotalMinutes
            : 0;

        SendEvent(
            EventTypes.StatusUp,
            FormatEndpointSubject($"{endpoint.Id}"),
            new
            {
                endpointId = endpoint.Id,
                endpointName = endpoint.Name,
                downAsOf = downAsOf,
                downtimeMinutes = downtimeMinutes,
                recoveredAt = DateTimeOffset.UtcNow
            }
        );
    }

    public virtual void NotifyDown(Endpoint endpoint, DateTimeOffset downAsOf, Exception? reason)
    {
        SendEvent(
            EventTypes.StatusDown,
            FormatEndpointSubject($"{endpoint.Id}"),
            new
            {
                endpointId = endpoint.Id,
                endpointName = endpoint.Name,
                downAsOf = downAsOf,
                reason = reason?.Message,
                reasonType = reason?.GetType().Name
            }
        );
    }

    public virtual void NotifyMisc(Endpoint endpoint, NotificationType type)
    {
        var eventType = type switch
        {
            NotificationType.EndpointAdded => EventTypes.ManagementAdded,
            NotificationType.EndpointRemoved => EventTypes.ManagementRemoved,
            NotificationType.EndpointEnabled => EventTypes.ManagementEnabled,
            NotificationType.EndpointDisabled => EventTypes.ManagementDisabled,
            NotificationType.EndpointReconfigured => EventTypes.ManagementReconfigured,
            _ => EventTypes.ManagementUnknown
        };

        SendEvent(
            eventType,
            FormatEndpointSubject($"{endpoint.Id}"),
            new
            {
                endpointId = endpoint.Id,
                endpointName = endpoint.Name,
                notificationType = type.ToString(),
                timestamp = DateTimeOffset.UtcNow
            }
        );
    }

    private async Task SendEventAsync(string eventType, string subject, object data)
    {
        try
        {
            if (!IsConfigured())
            {
                _logger.LogDebug("Event Grid notification service is not configured, skipping event");
                return;
            }

            var topicEndpoint = new Uri(_settings.Notifications_EventGrid_TopicEndpoint!);
            var accessKey = _settings.Notifications_EventGrid_AccessKey!;

            var eventGridEvent = new EventGridEvent(
                subject: subject,
                eventType: eventType,
                dataVersion: "1.0",
                data: BinaryData.FromObjectAsJson(data)
            );

            await _eventGridWrapper.SendEventAsync(topicEndpoint, accessKey, eventGridEvent);

            _logger.LogDebug("Successfully sent Event Grid event: {EventType} for {Subject}", eventType, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Event Grid notification for event type {EventType} and subject {Subject}", eventType, subject);
        }
    }

    private void SendEvent(string eventType, string subject, object data)
    {
        _bgExec.Execute(() => SendEventAsync(eventType, subject, data).Wait());
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.Notifications_EventGrid_TopicEndpoint)
            && !string.IsNullOrWhiteSpace(_settings.Notifications_EventGrid_AccessKey);
    }
}
