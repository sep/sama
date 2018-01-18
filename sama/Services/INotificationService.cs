using sama.Models;
using System;

namespace sama.Services
{
    public enum NotificationType
    {
        EndpointAdded,
        EndpointRemoved,
        EndpointEnabled,
        EndpointDisabled,
        EndpointReconfigured,
    }

    public interface INotificationService
    {
        void NotifySingleResult(Endpoint endpoint, EndpointCheckResult result);

        void NotifyUp(Endpoint endpoint, DateTimeOffset? downAsOf);

        void NotifyDown(Endpoint endpoint, DateTimeOffset downAsOf, Exception reason);

        void NotifyMisc(Endpoint endpoint, NotificationType type);
    }
}
