using Novell.Directory.Ldap;

namespace sama.Services
{
    /// <summary>
    /// This is a wrapper around lower-level LDAP functionality that cannot be (easily) tested.
    /// </summary>
    public class LdapAuthWrapper
    {
        public class LdapUser
        {
            public string DistinguishedName { get; set; }
            public string DisplayName { get; set; }
        }

        public virtual LdapUser Authenticate(string host, int port, bool useSsl, string bindDn, string bindPassword, string searchBaseDn, string searchFilter, string nameAttribute, RemoteCertificateValidationCallback certValidator)
        {
            using (var ldap = new LdapConnection())
            {
                ldap.SecureSocketLayer = useSsl;
                if (certValidator != null)
                    ldap.UserDefinedServerCertValidationDelegate += certValidator;
                ldap.Connect(host, port);
                ldap.Bind(LdapConnection.Ldap_V3, bindDn, bindPassword);

                var results = ldap.Search(searchBaseDn,
                    LdapConnection.SCOPE_SUB,
                    searchFilter,
                    new[] { nameAttribute },
                    false);
                var entry = results.next();

                return new LdapUser
                {
                    DistinguishedName = entry.DN,
                    DisplayName = entry.getAttribute(nameAttribute).StringValue
                };
            }
        }
    }
}
