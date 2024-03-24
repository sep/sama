﻿using Microsoft.Extensions.DependencyInjection;
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
        private readonly AggregateNotificationService _notifier;
        private readonly ConcurrentDictionary<int, EndpointStatus> _endpointStates = new ConcurrentDictionary<int, EndpointStatus>();

        public StateService(IServiceProvider provider, AggregateNotificationService notifier)
        {
            _provider = provider;
            _notifier = notifier;
        }

        public virtual void SetEndpointCheckInProgress(int endpointId)
        {
            _endpointStates.AddOrUpdate(endpointId, new EndpointStatus { InProgressResults = new List<EndpointCheckResult>() }, (id, oldStatus) =>
            {
                var newStatus = oldStatus.DeepClone();
                newStatus.InProgressResults ??= new List<EndpointCheckResult>();
                return newStatus;
            });
        }

        public virtual void AddEndpointCheckResult(int endpointId, EndpointCheckResult result, bool isFinal)
        {
            var status = _endpointStates.AddOrUpdate(endpointId, (id) =>
            {
                var es = new EndpointStatus();
                es.InProgressResults = new List<EndpointCheckResult> { result };
                return es;
            },
            (id, oldStatus) =>
            {
                var es = oldStatus.DeepClone();

                if (es.InProgressResults == null)
                    es.InProgressResults = new List<EndpointCheckResult>();

                es.InProgressResults.Add(result);
                return es;
            });

            PostProcessStatus(endpointId, status, isFinal);
        }

        public virtual IReadOnlyDictionary<Endpoint, EndpointStatus?> GetAll()
        {
            using var scope = _provider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var endpoints = dbContext.Endpoints.ToList();
            var states = endpoints.ToDictionary((e) => e, (e) => GetStatus(e.Id));
            return new ReadOnlyDictionary<Endpoint, EndpointStatus?>(states);
        }

        public virtual EndpointStatus? GetStatus(int id)
        {
            if (_endpointStates.TryGetValue(id, out EndpointStatus? status))
            {
                return status.DeepClone();
            }
            return null;
        }

        public virtual void RemoveStatus(int id)
        {
            _endpointStates.TryRemove(id, out EndpointStatus? _);
        }

        private void PostProcessStatus(int endpointId, EndpointStatus status, bool finalizeResults)
        {
            var endpoint = GetEndpointById(endpointId);
            if (endpoint == null) return;

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
                status.DownAsOf ??= status.LastFinishedResults?.FirstOrDefault(r => !r.Success)?.Start;
            }
        }

        private Endpoint? GetEndpointById(int endpointId)
        {
            using var scope = _provider.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return dbContext.Endpoints.FirstOrDefault(ep => ep.Id == endpointId);
        }

        private void NotifyNewestCheckResult(Endpoint endpoint, EndpointCheckResult? endpointCheckResult)
        {
            if (endpointCheckResult == null) return;

            _notifier.NotifySingleResult(endpoint, endpointCheckResult);
        }

        private void NotifyUpDown(Endpoint endpoint, EndpointStatus status)
        {
            if (status.InProgressResults == null || !status.InProgressResults.Any()) return;

            if (status.InProgressResults.Last().Success)
            {
                if (status.IsUp == false || status.IsUp == null)
                {
                    // Endpoint has just come up
                    _notifier.NotifyUp(endpoint, status.DownAsOf);
                }
                // If old status is true, don't notify that it's up
            }
            else
            {
                if (status.IsUp == null || status.IsUp == true)
                {
                    // Endpoint has just gone down
                    _notifier.NotifyDown(endpoint, status.DownAsOf ?? status.InProgressResults.Last().Start, status.InProgressResults.Last().Error);
                }
                else if (status.LastFinishedResults?.Last().Error?.ToString() != status.InProgressResults.Last().Error?.ToString())
                {
                    // Endpoint is down, but with a different error
                    _notifier.NotifyDown(endpoint, status.DownAsOf ?? status.InProgressResults.Last().Start, status.InProgressResults.Last().Error);
                }
            }
        }
    }
}
