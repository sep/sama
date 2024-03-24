using sama.Models;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace sama.Extensions
{
    public static class EndpointIcmpExtensions
    {
        private const string IcmpAddressKey = "Address";

        public static string? GetIcmpAddress(this Endpoint endpoint)
        {
            EnsureIcmp(endpoint);
            if (string.IsNullOrWhiteSpace(endpoint.JsonConfig)) return null;

            var node = JsonNode.Parse(endpoint.JsonConfig);
            var matches = node?.AsObject().Where(kvp => kvp.Key == IcmpAddressKey);
            if (matches?.Any() ?? false)
            {
                if (matches.First().Value == null) return null;
                return matches.First().Value!.GetValue<string>();
            }
            // else
            return null;
        }

        public static void SetIcmpAddress(this Endpoint endpoint, string? address)
        {
            EnsureIcmp(endpoint);

            var json = (string.IsNullOrWhiteSpace(endpoint.JsonConfig) ? "{}" : endpoint.JsonConfig);

            var nodeObj = JsonNode.Parse(json)?.AsObject() ?? throw new ArgumentException($"Unable to deserialize Address");
            nodeObj.Remove(IcmpAddressKey);
            nodeObj.Add(IcmpAddressKey, JsonValue.Create(address));
            endpoint.JsonConfig = nodeObj.ToJsonString();
        }

        private static void EnsureIcmp(Endpoint endpoint)
        {
            if (endpoint.Kind != Endpoint.EndpointKind.Icmp)
                throw new ArgumentException("Endpoint is not Ping.", nameof(endpoint));
        }
    }
}
