using sama.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace sama.Services
{
    public class AggregateNotificationService
    {
        private readonly List<INotificationService> _notificationServices;

        public AggregateNotificationService(IEnumerable<INotificationService> notificationServices)
        {
            _notificationServices = notificationServices.ToList();
        }

        public virtual void NotifySingleResult(Endpoint endpoint, EndpointCheckResult result)
        {
            _notificationServices
                .ForEach(ns => ns.NotifySingleResult(endpoint, result));
        }

        public virtual void NotifyUp(Endpoint endpoint, DateTimeOffset? downAsOf)
        {
            _notificationServices
                .ForEach(ns => ns.NotifyUp(endpoint, downAsOf));
        }

        public virtual void NotifyDown(Endpoint endpoint, DateTimeOffset downAsOf, Exception reason)
        {
            _notificationServices
                .ForEach(ns => ns.NotifyDown(endpoint, downAsOf, reason));
        }

        public virtual void NotifyMisc(Endpoint endpoint, NotificationType type)
        {
            _notificationServices
                .ForEach(ns => ns.NotifyMisc(endpoint, type));
        }
    }
}
