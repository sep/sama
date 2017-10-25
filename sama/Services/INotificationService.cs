using sama.Models;
using System;

namespace sama.Services
{
    public interface INotificationService
    {
        void NotifySingleResult(Endpoint endpoint, EndpointCheckResult result);

        void NotifyUp(Endpoint endpoint, DateTimeOffset? downAsOf);

        void NotifyDown(Endpoint endpoint, DateTimeOffset downAsOf, Exception reason);
    }
}
