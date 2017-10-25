using Microsoft.Extensions.DependencyInjection;
using sama.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace sama.Services
{
    public class StateService
    {
        private readonly IServiceProvider _provider;
        private readonly ConcurrentDictionary<int, EndpointStatus> _endpointStates = new ConcurrentDictionary<int, EndpointStatus>();

        public StateService(IServiceProvider provider)
        {
            _provider = provider;
        }

        public virtual void SetEndpointCheckInProgress(int endpointId)
        {
            _endpointStates.AddOrUpdate(endpointId, new EndpointStatus { InProgressResults = new List<EndpointCheckResult>() }, (id, oldStatus) =>
            {
                var newStatus = oldStatus.DeepClone();
                newStatus.InProgressResults = (newStatus.InProgressResults ?? new List<EndpointCheckResult>());
                return newStatus;
            });
        }

        public virtual void AddEndpointCheckResult(int endpointId, EndpointCheckResult result, bool isFinal)
        {
            EndpointStatus status = null;

            _endpointStates.AddOrUpdate(endpointId, (id) =>
            {
                status = new EndpointStatus();
                status.InProgressResults = new List<EndpointCheckResult> { result };
                return status;
            },
            (id, oldStatus) =>
            {
                status = oldStatus.DeepClone();

                if (status.InProgressResults == null)
                    status.InProgressResults = new List<EndpointCheckResult>();

                status.InProgressResults.Add(result);
                return status;
            });

            PostProcessStatus(endpointId, status, isFinal);
        }

        public virtual IReadOnlyDictionary<Endpoint, EndpointStatus> GetAll()
        {
            List<Endpoint> endpoints;
            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                endpoints = dbContext.Endpoints.ToList();
            }

            var states = endpoints.ToDictionary((e) => e, (e) => GetStatus(e.Id));
            return new ReadOnlyDictionary<Endpoint, EndpointStatus>(states);
        }

        public virtual EndpointStatus GetStatus(int id)
        {
            if (_endpointStates.TryGetValue(id, out EndpointStatus status))
            {
                return status.DeepClone();
            }
            return null;
        }

        public virtual void RemoveStatus(int id)
        {
            _endpointStates.TryRemove(id, out EndpointStatus _);
        }

        private void PostProcessStatus(int endpointId, EndpointStatus status, bool finalizeResults)
        {
            var endpoint = GetEndpointById(endpointId);
            NotifyNewestCheckResult(endpoint, status.InProgressResults?.LastOrDefault());

            if (finalizeResults)
            {
                NotifyUpDown(endpoint, status);
                status.LastFinishedResults = status.InProgressResults;
                status.InProgressResults = null;
            }

            if (status.IsUp == null || status.IsUp == true)
            {
                status.DownAsOf = null;
            }
            else
            {
                status.DownAsOf = status.DownAsOf ?? status.LastFinishedResults?.FirstOrDefault(r => !r.Success)?.Start;
            }
        }

        private Endpoint GetEndpointById(int endpointId)
        {
            using (var scope = _provider.CreateScope())
            using (var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())
            {
                return dbContext.Endpoints.FirstOrDefault(ep => ep.Id == endpointId);
            }
        }

        private void NotifyNewestCheckResult(Endpoint endpoint, EndpointCheckResult endpointCheckResult)
        {
            _provider.GetServices<INotificationService>()
                .ToList()
                .ForEach(n => n.NotifySingleResult(endpoint, endpointCheckResult));
        }

        private void NotifyUpDown(Endpoint endpoint, EndpointStatus status)
        {
            if (status.InProgressResults.Last().Success)
            {
                if (status.IsUp == false)
                {
                    // Endpoint has just come up
                    _provider.GetServices<INotificationService>()
                        .ToList()
                        .ForEach(n => n.NotifyUp(endpoint, status.DownAsOf));
                }
                // If old status is true or null, don't notify that it's up
            }
            else
            {
                if (status.IsUp == null || status.IsUp == true)
                {
                    // Endpoint has just gone down
                    _provider.GetServices<INotificationService>()
                        .ToList()
                        .ForEach(n => n.NotifyDown(endpoint, status.DownAsOf ?? status.InProgressResults.Last().Start, status.InProgressResults.Last().Error));
                }
                else if (status.LastFinishedResults?.Last().Error?.ToString() != status.InProgressResults.Last().Error?.ToString())
                {
                    // Endpoint is down, but with a different error
                    _provider.GetServices<INotificationService>()
                       .ToList()
                       .ForEach(n => n.NotifyDown(endpoint, status.DownAsOf ?? status.InProgressResults.Last().Start, status.InProgressResults.Last().Error));
                }
            }
        }
    }
}
