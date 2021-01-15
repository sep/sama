using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using sama.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

namespace sama.Extensions
{
    public static class EndpointHttpExtensions
    {
        private static JsonSerializerSettings JsonSettings = new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() };

        public static string? GetHttpLocation(this Endpoint endpoint) =>
            GetValue<string>(endpoint, "Location");

        public static void SetHttpLocation(this Endpoint endpoint, string? location) =>
            SetValue(endpoint, "Location", location);

        public static string? GetHttpResponseMatch(this Endpoint endpoint) =>
            GetValue<string>(endpoint, "ResponseMatch");

        public static void SetHttpResponseMatch(this Endpoint endpoint, string? responseMatch) =>
            SetValue(endpoint, "ResponseMatch", responseMatch);

        public static List<int>? GetHttpStatusCodes(this Endpoint endpoint) =>
            GetValueList<int>(endpoint, "StatusCodes");

        public static void SetHttpStatusCodes(this Endpoint endpoint, List<int> codes) =>
            SetValue(endpoint, "StatusCodes", codes);

        public static bool GetHttpIgnoreTlsCerts(this Endpoint endpoint) =>
            GetValue<bool>(endpoint, "IgnoreTlsCertificates");

        public static void SetHttpIgnoreTlsCerts(this Endpoint endpoint, bool ignore) =>
            SetValue(endpoint, "IgnoreTlsCertificates", ignore);

        public static string? GetHttpCustomTlsCert(this Endpoint endpoint) =>
            GetValue<string>(endpoint, "CustomTlsCertificate");

        public static void SetHttpCustomTlsCert(this Endpoint endpoint, string? pemEncodedCert) =>
            SetValue(endpoint, "CustomTlsCertificate", pemEncodedCert);


        private static List<T>? GetValueList<T>(Endpoint endpoint, string name, List<T>? defaultValue = null)
        {
            var list = GetValue<List<object>>(endpoint, name);
            if (list == null)
            {
                return defaultValue;
            }
            return list.Select(o => (T)Convert.ChangeType(o, typeof(T))).ToList();
        }

        private static T? GetValue<T>(Endpoint endpoint, string name, T? defaultValue = default(T))
        {
            EnsureHttp(endpoint);
            if (string.IsNullOrWhiteSpace(endpoint.JsonConfig)) return defaultValue;

            var obj = JsonConvert.DeserializeObject<ExpandoObject>(endpoint.JsonConfig, JsonSettings) as IDictionary<string, object>;
            if (obj == null)
            {
                throw new ArgumentException($"Unable to get value for '{name}'");
            }
            if (!obj.ContainsKey(name))
            {
                return defaultValue;
            }
            return (T)obj[name];
        }

        private static void SetValue<T>(Endpoint endpoint, string name, T? value)
        {
            EnsureHttp(endpoint);

            var json = (string.IsNullOrWhiteSpace(endpoint.JsonConfig) ? "{}" : endpoint.JsonConfig);
            var obj = JsonConvert.DeserializeObject<ExpandoObject>(json, JsonSettings) ?? throw new ArgumentException($"Unable to deserialize '{name}'");
            obj!.Remove(name, out object _);
            if (!obj.TryAdd(name, value))
            {
                throw new ArgumentException($"Unable to set value for '{name}'");
            }
            endpoint.JsonConfig = JsonConvert.SerializeObject(obj, JsonSettings);
        }

        private static void EnsureHttp(Endpoint endpoint)
        {
            if (endpoint.Kind != Endpoint.EndpointKind.Http)
                throw new ArgumentException("Endpoint is not HTTP.", nameof(endpoint));
        }
    }
}
