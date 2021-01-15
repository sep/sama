using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using sama.Models;
using System;
using System.Dynamic;

namespace sama.Extensions
{
    public static class EndpointIcmpExtensions
    {
        private static JsonSerializerSettings JsonSettings = new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() };

        public static string? GetIcmpAddress(this Endpoint endpoint)
        {
            EnsureIcmp(endpoint);
            if (string.IsNullOrWhiteSpace(endpoint.JsonConfig)) return null;

            return JsonConvert.DeserializeObject<dynamic>(endpoint.JsonConfig, JsonSettings)?.Address?.ToObject<string>();
        }

        public static void SetIcmpAddress(this Endpoint endpoint, string? address)
        {
            EnsureIcmp(endpoint);

            var json = (string.IsNullOrWhiteSpace(endpoint.JsonConfig) ? "{}" : endpoint.JsonConfig);
            dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(json, JsonSettings) ?? throw new ArgumentException("Unable to deserialize JSON settings for endpoint");
            obj.Address = address;
            endpoint.JsonConfig = JsonConvert.SerializeObject(obj, JsonSettings);
        }

        private static void EnsureIcmp(Endpoint endpoint)
        {
            if (endpoint.Kind != Endpoint.EndpointKind.Icmp)
                throw new ArgumentException("Endpoint is not Ping.", nameof(endpoint));
        }
    }
}
