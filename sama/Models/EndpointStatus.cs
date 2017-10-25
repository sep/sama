using System;
using System.Collections.Generic;
using System.Linq;

namespace sama.Models
{
    public class EndpointStatus
    {
        public DateTimeOffset? DownAsOf { get; set; }
        public List<EndpointCheckResult> InProgressResults { get; set; }
        public List<EndpointCheckResult> LastFinishedResults { get; set; }

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

        public Exception Error
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
