using System;
using sama.Models;
using System.Net.NetworkInformation;
using sama.Extensions;

namespace sama.Services
{
    public class IcmpCheckService : ICheckService
    {
        private readonly PingWrapper _pingWrapper;

        public IcmpCheckService(PingWrapper pingWrapper)
        {
            _pingWrapper = pingWrapper;
        }

        public bool CanHandle(Endpoint endpoint)
        {
            return (endpoint.Kind == Endpoint.EndpointKind.Icmp);
        }

        public bool Check(Endpoint endpoint, out string failureMessage)
        {
            try
            {
                var result = _pingWrapper.SendPing(endpoint.GetIcmpAddress());
                if (result == IPStatus.Success)
                {
                    failureMessage = null;
                    return true;
                }
                else
                {
                    failureMessage = GetFriendlyError(result);
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType() == typeof(PingException) && ex.InnerException != null)
                    ex = ex.InnerException;

                failureMessage = $"Unable to ping: {ex.Message}.";
                return false;
            }
        }

        private string GetFriendlyError(IPStatus status)
        {
            switch (status)
            {
                case IPStatus.BadDestination:
                    return "The destination IP address cannot receive ping requests.";
                case IPStatus.BadHeader:
                    return "The ping request header is invalid.";
                case IPStatus.BadOption:
                    return "The ping request contains an invalid option.";
                case IPStatus.BadRoute:
                    return "There is no valid route to the destination.";
                case IPStatus.DestinationHostUnreachable:
                    return "The destination host is unreachable.";
                case IPStatus.DestinationNetworkUnreachable:
                    return "The destination network is unreachable.";
                case IPStatus.DestinationPortUnreachable:
                    return "The destination port is unreachable.";
                case IPStatus.DestinationProhibited:
                    return "Contact with the destination host is prohibited.";
                case IPStatus.DestinationScopeMismatch:
                    return "The destination host is in a different scope.";
                case IPStatus.DestinationUnreachable:
                    return "The destination is unreachable.";
                case IPStatus.HardwareError:
                    return "A hardware error has occurred.";
                case IPStatus.IcmpError:
                    return "A protocol error hs occurred.";
                case IPStatus.NoResources:
                    return "Insufficient network resources.";
                case IPStatus.PacketTooBig:
                    return "The packet is too big.";
                case IPStatus.ParameterProblem:
                    return "A node has encountered a problem while processing the packet header.";
                case IPStatus.SourceQuench:
                    return "The ping request was discarded.";
                case IPStatus.TimedOut:
                    return "The ping request timed out.";
                case IPStatus.TimeExceeded:
                    return "The ping request TTL was exceeded.";
                case IPStatus.TtlExpired:
                    return "The ping request TTL expired.";
                default:
                    return "An unknown error occurred.";
            }
        }
    }
}
