using Microsoft.Extensions.Configuration;
using sama.Models;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace sama.Services
{
    public class LdapService : IDisposable
    {
        private readonly IConfigurationRoot _config;
        private readonly LdapAuthWrapper _ldapWrapper;

        public LdapService(IConfigurationRoot config, LdapAuthWrapper ldapWrapper)
        {
            _config = config;
            _ldapWrapper = ldapWrapper;
        }

        public virtual ApplicationUser Authenticate(string username, string password)
        {
            try
            {
                var ldapSettings = _config.GetSection("SAMA").GetSection("LDAP");

                var ldapUser = _ldapWrapper.Authenticate(
                        ldapSettings.GetValue<string>("Host"),
                        ldapSettings.GetValue<int>("Port"),
                        ldapSettings.GetValue<bool>("SSL"),
                        string.Format(ldapSettings.GetValue<string>("BindDnFormat"), username),
                        password,
                        ldapSettings.GetValue<string>("SearchBaseDn"),
                        string.Format(ldapSettings.GetValue<string>("SearchFilterFormat"), username),
                        ldapSettings.GetValue<string>("NameAttribute"),
                        Ldap_UserDefinedServerCertValidationDelegate
                    );

                return new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = ldapUser.DisplayName,
                    IsRemote = true
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public virtual bool IsLdapEnabled()
        {
            return _config.GetSection("SAMA").GetSection("LDAP").GetValue<bool>("Enabled");
        }

        public void Dispose()
        {
        }

        private bool Ldap_UserDefinedServerCertValidationDelegate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
