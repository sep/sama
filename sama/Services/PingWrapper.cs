using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;

namespace sama.Services
{
    /// <summary>
    /// This is a wrapper around lower-level Ping functionality that cannot be (easily) tested.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PingWrapper
    {
        public virtual (IPStatus, TimeSpan) SendPing(string address)
        {
            using(var ping = new Ping())
            {
                var result = ping.Send(address);
                return (result.Status, TimeSpan.FromMilliseconds(result.RoundtripTime));
            }
        }
    }
}
