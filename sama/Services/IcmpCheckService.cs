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

        public EndpointCheckResult Check(Endpoint endpoint)
        {
            var checkResult = new EndpointCheckResult { Start = DateTimeOffset.UtcNow };

            try
            {
                var (status, roundtripTime) = _pingWrapper.SendPing(endpoint.GetIcmpAddress());
                if (status == IPStatus.Success)
                {
                    checkResult.Success = true;
                    checkResult.Stop = DateTimeOffset.UtcNow;
                    checkResult.ResponseTime = roundtripTime;
                    return checkResult;
                }
                else
                {
                    checkResult.Error = new Exception(GetFriendlyError(status));
                    checkResult.Success = false;
                    checkResult.Stop = DateTimeOffset.UtcNow;
                    checkResult.ResponseTime = null;
                    return checkResult;
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType() == typeof(PingException) && ex.InnerException != null)
                    ex = ex.InnerException;

                checkResult.Error = new Exception($"Unable to ping: {ex.Message}", ex);
                checkResult.Success = false;
                checkResult.Stop = DateTimeOffset.UtcNow;
                checkResult.ResponseTime = null;
                return checkResult;
            }
        }

        private string GetFriendlyError(IPStatus status)
        {
            switch (status)
            {
                case IPStatus.BadDestination:
                    return "The destination IP address cannot receive ping requests";
                case IPStatus.BadHeader:
                    return "The ping request header is invalid";
                case IPStatus.BadOption:
                    return "The ping request contains an invalid option";
                case IPStatus.BadRoute:
                    return "There is no valid route to the destination";
                case IPStatus.DestinationHostUnreachable:
                    return "The destination host is unreachable";
                case IPStatus.DestinationNetworkUnreachable:
                    return "The destination network is unreachable";
                case IPStatus.DestinationPortUnreachable:
                    return "The destination port is unreachable";
                case IPStatus.DestinationProhibited:
                    return "Contact with the destination host is prohibited";
                case IPStatus.DestinationScopeMismatch:
                    return "The destination host is in a different scope";
                case IPStatus.DestinationUnreachable:
                    return "The destination is unreachable";
                case IPStatus.HardwareError:
                    return "A hardware error has occurred";
                case IPStatus.IcmpError:
                    return "A protocol error hs occurred";
                case IPStatus.NoResources:
                    return "Insufficient network resources";
                case IPStatus.PacketTooBig:
                    return "The packet is too big";
                case IPStatus.ParameterProblem:
                    return "A node has encountered a problem while processing the packet header";
                case IPStatus.SourceQuench:
                    return "The ping request was discarded";
                case IPStatus.TimedOut:
                    return "The ping request timed out";
                case IPStatus.TimeExceeded:
                    return "The ping request TTL was exceeded";
                case IPStatus.TtlExpired:
                    return "The ping request TTL expired";
                default:
                    return "An unknown error occurred";
            }
        }
    }
}
