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
    }
}
