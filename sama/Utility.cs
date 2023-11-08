using System;
using System.Net;
using System.Net.Http;

namespace sama
{
    public class Utility
    {
        public static string KindString(Models.Endpoint.EndpointKind kind)
        {
            if (kind == Models.Endpoint.EndpointKind.Http)
                return "HTTP";
            else if (kind == Models.Endpoint.EndpointKind.Icmp)
                return "Ping";
            else
                return "Unknown";
        }

        public static Version GetConfiguredHttpRequestVersion(string? configString)
        {
            var fallback = HttpVersion.Version11;

            if (!string.IsNullOrWhiteSpace(configString) && configString.Contains('_'))
            {
                var versionPart = configString.Split('_')[0];
                return versionPart switch
                {
                    "10" => HttpVersion.Version10,
                    "11" => HttpVersion.Version11,
                    "20" => HttpVersion.Version20,
                    "30" => HttpVersion.Version30,
                    _ => fallback,
                };
            }

            return fallback;
        }

        public static HttpVersionPolicy GetConfiguredHttpVersionPolicy(string? configString)
        {
            var fallback = HttpVersionPolicy.RequestVersionOrLower;

            if (!string.IsNullOrWhiteSpace(configString) && configString.Contains('_'))
            {
                var policyPart = configString.Split('_')[1];
                return policyPart switch
                {
                    "OrLower" => HttpVersionPolicy.RequestVersionOrLower,
                    "Exact" => HttpVersionPolicy.RequestVersionExact,
                    "OrHigher" => HttpVersionPolicy.RequestVersionOrHigher,
                    _ => fallback,
                };
            }

            return fallback;
        }
    }
}
