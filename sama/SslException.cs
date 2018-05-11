using System;

namespace sama
{
    public class SslException : Exception
    {
        public string Details { get; private set; }

        public SslException(string details)
            : base("Could not establish a secure LDAP connection")
        {
            Details = details;
        }
    }
}
