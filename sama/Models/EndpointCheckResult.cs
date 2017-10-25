using System;

namespace sama.Models
{
    public class EndpointCheckResult
    {
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset Stop { get; set; }
        public TimeSpan? ResponseTime { get; set; }
        public bool Success { get; set; }
        public Exception Error { get; set; }

        public EndpointCheckResult Clone()
        {
            return (EndpointCheckResult)MemberwiseClone();
        }
    }
}
