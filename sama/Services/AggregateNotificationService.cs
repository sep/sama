using sama.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace sama.Services
{
    public class AggregateNotificationService
    {
        private readonly List<INotificationService> _notificationServices;
        private readonly BackgroundExecutionWrapper _bgExec;

        public AggregateNotificationService(IEnumerable<INotificationService> notificationServices, BackgroundExecutionWrapper bgExec)
        {
            _notificationServices = notificationServices.ToList();
            _bgExec = bgExec;
        }

        public virtual void NotifySingleResult(Endpoint endpoint, EndpointCheckResult result)
        {
            _bgExec.Execute(() => _notificationServices
                .ForEach(ns => ns.NotifySingleResult(endpoint, result)));
        }

        public virtual void NotifyUp(Endpoint endpoint, DateTimeOffset? downAsOf)
        {
            _bgExec.Execute(() => _notificationServices
                .ForEach(ns => ns.NotifyUp(endpoint, downAsOf)));
        }

        public virtual void NotifyDown(Endpoint endpoint, DateTimeOffset downAsOf, Exception? reason)
        {
            _bgExec.Execute(() => _notificationServices
                .ForEach(ns => ns.NotifyDown(endpoint, downAsOf, reason)));
        }

        public virtual void NotifyMisc(Endpoint endpoint, NotificationType type)
        {
            _bgExec.Execute(() => _notificationServices
                .ForEach(ns => ns.NotifyMisc(endpoint, type)));
        }
    }
}
