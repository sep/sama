using System.Net.NetworkInformation;

namespace sama.Services
{
    /// <summary>
    /// This is a wrapper around lower-level Ping functionality that cannot be (easily) tested.
    /// </summary>
    public class PingWrapper
    {
        public virtual IPStatus SendPing(string address)
        {
            using(var ping = new Ping())
            {
                return ping.Send(address).Status;
            }
        }
    }
}
