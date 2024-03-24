using sama.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace sama.Extensions
{
    public static class EndpointHttpExtensions
    {
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
            EnsureHttp(endpoint);
            if (string.IsNullOrWhiteSpace(endpoint.JsonConfig)) return defaultValue;

            var node = JsonNode.Parse(endpoint.JsonConfig);
            var matches = node?.AsObject().Where(kvp => kvp.Key == name);
            if (matches?.Any() ?? false)
            {
                if (matches.First().Value == null) return defaultValue;
                var array = matches.First().Value!.AsArray();
                if (array == null) return defaultValue;
                return array.Select(n => n!.GetValue<T>()).ToList();
            }
            // else
            return defaultValue;
        }

        private static T? GetValue<T>(Endpoint endpoint, string name, T? defaultValue = default(T))
        {
            EnsureHttp(endpoint);
            if (string.IsNullOrWhiteSpace(endpoint.JsonConfig)) return defaultValue;

            var node = JsonNode.Parse(endpoint.JsonConfig);
            var matches = node?.AsObject().Where(kvp => kvp.Key == name);
            if (matches?.Any() ?? false)
            {
                if (matches.First().Value == null) return defaultValue;
                return matches.First().Value!.GetValue<T>();
            }
            // else
            return defaultValue;
        }

        private static void SetValue<T>(Endpoint endpoint, string name, T? value)
        {
            EnsureHttp(endpoint);

            var json = (string.IsNullOrWhiteSpace(endpoint.JsonConfig) ? "{}" : endpoint.JsonConfig);

            var nodeObj = JsonNode.Parse(json)?.AsObject() ?? throw new ArgumentException($"Unable to deserialize '{name}'");
            nodeObj.Remove(name);
            nodeObj.Add(name, JsonValue.Create(value));
            endpoint.JsonConfig = nodeObj.ToJsonString();
        }

        private static void EnsureHttp(Endpoint endpoint)
        {
            if (endpoint.Kind != Endpoint.EndpointKind.Http)
                throw new ArgumentException("Endpoint is not HTTP.", nameof(endpoint));
        }
    }
}
