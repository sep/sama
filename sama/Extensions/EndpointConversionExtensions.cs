using sama.Models;
using System;
using System.Linq;

namespace sama.Extensions
{
    public static class EndpointConversionExtensions
    {
        public static EndpointViewModel ToEndpointViewModel(this Endpoint endpoint)
        {
            EndpointViewModel vm;
            if (endpoint.Kind == Endpoint.EndpointKind.Http)
            {
                vm = new HttpEndpointViewModel
                {
                    Location = endpoint.GetHttpLocation(),
                    ResponseMatch = endpoint.GetHttpResponseMatch(),
                    StatusCodes = string.Join(',', endpoint.GetHttpStatusCodes()?.Select(c => c.ToString()) ?? new string[0]),
                    IgnoreCerts = endpoint.GetHttpIgnoreTlsCerts(),
                    CustomCert = endpoint.GetHttpCustomTlsCert()
                };
            }
            else if (endpoint.Kind == Endpoint.EndpointKind.Icmp)
            {
                vm = new IcmpEndpointViewModel
                {
                    Address = endpoint.GetIcmpAddress()
                };
            }
            else
            {
                throw new NotImplementedException();
            }

            vm.Id = endpoint.Id;
            vm.Kind = endpoint.Kind;
            vm.Enabled = endpoint.Enabled;
            vm.Name = endpoint.Name;

            return vm;
        }

        public static Endpoint ToEndpoint(this EndpointViewModel vm)
        {
            var endpoint = new Endpoint
            {
                Id = vm.Id,
                Enabled = vm.Enabled,
                Name = vm.Name,
                Kind = vm.Kind,
                LastUpdated = DateTimeOffset.UtcNow
            };

            if (vm.Kind == Endpoint.EndpointKind.Http)
            {
                var httpVm = (HttpEndpointViewModel)vm;

                endpoint.SetHttpLocation(httpVm.Location);
                endpoint.SetHttpResponseMatch(httpVm.ResponseMatch);

                if (!string.IsNullOrWhiteSpace(httpVm.StatusCodes))
                    endpoint.SetHttpStatusCodes(httpVm.StatusCodes.Split(',').Select(code => int.Parse(code.Trim())).ToList());

                endpoint.SetHttpIgnoreTlsCerts(httpVm.IgnoreCerts);
                endpoint.SetHttpCustomTlsCert(httpVm.CustomCert);
            }
            else if (vm.Kind == Endpoint.EndpointKind.Icmp)
            {
                var icmpVm = (IcmpEndpointViewModel)vm;

                endpoint.SetIcmpAddress(icmpVm.Address);
            }
            else
            {
                throw new NotImplementedException();
            }

            return endpoint;
        }
    }
}
