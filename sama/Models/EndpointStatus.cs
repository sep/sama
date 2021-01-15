using System;
using System.Collections.Generic;
using System.Linq;

namespace sama.Models
{
    public class EndpointStatus
    {
        /// <summary>
        /// If not null, this indicates the first time the endpoint was reported as being down
        /// after either an "up" report or application start
        /// </summary>
        public DateTimeOffset? DownAsOf { get; set; }

        /// <summary>
        /// If null, then no endpoint checks are currently (effectively) in progress.
        /// Otherwise, this stores check status(es), including any intermediate failures
        /// before the endpoint is actually reported as "down".
        /// </summary>
        public List<EndpointCheckResult>? InProgressResults { get; set; }

        /// <summary>
        /// This gets the data from <see cref="InProgressResults"/> after result finalization
        /// </summary>
        public List<EndpointCheckResult>? LastFinishedResults { get; set; }

        public EndpointStatus DeepClone()
        {
            return new EndpointStatus
            {
                DownAsOf = DownAsOf,
                InProgressResults = InProgressResults?.Select(r => r.Clone())?.ToList(),
                LastFinishedResults = LastFinishedResults?.Select(r => r.Clone())?.ToList()
            };
        }

        public bool? IsUp
        {
            get
            {
                return LastFinishedResults?.Last()?.Success;
            }
        }

        public bool IsInProgress
        {
            get
            {
                return (InProgressResults != null);
            }
        }

        public Exception? Error
        {
            get
            {
                return LastFinishedResults?.Last()?.Error;
            }
        }

        public DateTimeOffset LastUpdated
        {
            get
            {
                return LastFinishedResults?.Last()?.Stop ?? DateTimeOffset.MinValue;
            }
        }
    }
}
