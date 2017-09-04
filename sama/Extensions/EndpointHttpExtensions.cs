using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using sama.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace sama.Extensions
{
    public static class EndpointHttpExtensions
    {
        private static JsonSerializerSettings JsonSettings = new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() };

        public static string GetHttpLocation(this Endpoint endpoint)
        {
            EnsureHttp(endpoint);
            if (string.IsNullOrWhiteSpace(endpoint.JsonConfig)) return null;

            return JsonConvert.DeserializeObject<dynamic>(endpoint.JsonConfig, JsonSettings).Location?.ToObject<string>();
        }

        public static void SetHttpLocation(this Endpoint endpoint, string location)
        {
            EnsureHttp(endpoint);

            var json = (string.IsNullOrWhiteSpace(endpoint.JsonConfig) ? "{}" : endpoint.JsonConfig);
            dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(json, JsonSettings);
            obj.Location = location;
            endpoint.JsonConfig = JsonConvert.SerializeObject(obj, JsonSettings);
        }

        public static string GetHttpResponseMatch(this Endpoint endpoint)
        {
            EnsureHttp(endpoint);
            if (string.IsNullOrWhiteSpace(endpoint.JsonConfig)) return null;

            return JsonConvert.DeserializeObject<dynamic>(endpoint.JsonConfig, JsonSettings).ResponseMatch?.ToObject<string>();
        }

        public static void SetHttpResponseMatch(this Endpoint endpoint, string responseMatch)
        {
            EnsureHttp(endpoint);

            var json = (string.IsNullOrWhiteSpace(endpoint.JsonConfig) ? "{}" : endpoint.JsonConfig);
            dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(json, JsonSettings);
            obj.ResponseMatch = responseMatch;
            endpoint.JsonConfig = JsonConvert.SerializeObject(obj, JsonSettings);
        }

        public static List<int> GetHttpStatusCodes(this Endpoint endpoint)
        {
            EnsureHttp(endpoint);
            if (string.IsNullOrWhiteSpace(endpoint.JsonConfig)) return null;

            return JsonConvert.DeserializeObject<dynamic>(endpoint.JsonConfig, JsonSettings).StatusCodes?.ToObject<List<int>>();
        }

        public static void SetHttpStatusCodes(this Endpoint endpoint, List<int> codes)
        {
            EnsureHttp(endpoint);

            var json = (string.IsNullOrWhiteSpace(endpoint.JsonConfig) ? "{}" : endpoint.JsonConfig);
            dynamic obj = JsonConvert.DeserializeObject<ExpandoObject>(json, JsonSettings);
            obj.StatusCodes = codes;
            endpoint.JsonConfig = JsonConvert.SerializeObject(obj, JsonSettings);
        }

        private static void EnsureHttp(Endpoint endpoint)
        {
            if (endpoint.Kind != Endpoint.EndpointKind.Http)
                throw new ArgumentException("Endpoint is not HTTP.", nameof(endpoint));
        }
    }
}
