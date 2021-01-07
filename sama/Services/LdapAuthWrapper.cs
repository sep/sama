using Novell.Directory.Ldap;
using System.Diagnostics.CodeAnalysis;

namespace sama.Services
{
    /// <summary>
    /// This is a wrapper around lower-level LDAP functionality that cannot be (easily) tested.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class LdapAuthWrapper
    {
        public class LdapUser
        {
            public string DistinguishedName { get; set; }
            public string DisplayName { get; set; }
        }

        public virtual LdapUser Authenticate(string host, int port, bool useSsl, string bindDn, string bindPassword, string searchBaseDn, string searchFilter, string nameAttribute, RemoteCertificateValidationCallback certValidator)
        {
            using var ldap = new LdapConnection() { SecureSocketLayer = useSsl };
            if (certValidator != null)
                ldap.UserDefinedServerCertValidationDelegate += certValidator;
            ldap.Connect(host, port);
            ldap.Bind(LdapConnection.LdapV3, bindDn, bindPassword);

            var results = ldap.Search(searchBaseDn,
                LdapConnection.ScopeSub,
                searchFilter,
                new[] { nameAttribute },
                false);
            var entry = results.Next();

            return new LdapUser
            {
                DistinguishedName = entry.Dn,
                DisplayName = entry.GetAttribute(nameAttribute).StringValue
            };
        }
    }
}
