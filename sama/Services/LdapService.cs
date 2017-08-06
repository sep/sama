using Microsoft.Extensions.Configuration;
using Novell.Directory.Ldap;
using sama.Models;
using System;

namespace sama.Services
{
    public class LdapService
    {
        private readonly IConfigurationRoot _config;

        public LdapService(IConfigurationRoot config)
        {
            _config = config;
        }

        public ApplicationUser Authenticate(string username, string password)
        {
            try
            {
                using (var ldap = new LdapConnection())
                {
                    var ldapSettings = _config.GetSection("SAMA").GetSection("LDAP");

                    var formattedUsername = string.Format(ldapSettings.GetValue<string>("BindDnFormat"), username);
                    ldap.SecureSocketLayer = ldapSettings.GetValue<bool>("SSL");
                    ldap.UserDefinedServerCertValidationDelegate += Ldap_UserDefinedServerCertValidationDelegate;
                    ldap.Connect(ldapSettings.GetValue<string>("Host"), ldapSettings.GetValue<int>("Port"));
                    ldap.Bind(LdapConnection.Ldap_V3, formattedUsername, password);

                    var results = ldap.Search(ldapSettings.GetValue<string>("SearchBaseDn"),
                        LdapConnection.SCOPE_SUB,
                        string.Format(ldapSettings.GetValue<string>("SearchFilterFormat"), username),
                        new[] { ldapSettings.GetValue<string>("NameAttribute") },
                        false);
                    var entry = results.next();

                    return new ApplicationUser
                    {
                        Id = Guid.NewGuid(),
                        UserName = entry.getAttribute(ldapSettings.GetValue<string>("NameAttribute")).StringValue,
                        IsRemote = true
                    };
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool IsLdapEnabled()
        {
            return _config.GetSection("SAMA").GetSection("LDAP").GetValue<bool>("Enabled");
        }

        private bool Ldap_UserDefinedServerCertValidationDelegate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certificate, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
