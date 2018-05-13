using System;

namespace sama
{
    public class SslException : Exception
    {
        public string Details { get; private set; }

        private SslException(string message, string details)
            : base(message)
        {
            Details = details;
        }

        public static SslException CreateException(bool isLdap, string details)
        {
            var type = (isLdap ? "LDAP" : "HTTPS");
            return new SslException($"Could not establish a secure {type} connection", details);
        }
    }
}
