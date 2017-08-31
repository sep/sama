using sama.Models;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace sama.Services
{
    public class LdapService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly LdapAuthWrapper _ldapWrapper;

        public LdapService(SettingsService settingsService, LdapAuthWrapper ldapWrapper)
        {
            _settingsService = settingsService;
            _ldapWrapper = ldapWrapper;
        }

        public virtual ApplicationUser Authenticate(string username, string password)
        {
            try
            {
                var ldapUser = _ldapWrapper.Authenticate(
                        _settingsService.Ldap_Host,
                        _settingsService.Ldap_Port,
                        _settingsService.Ldap_Ssl,
                        string.Format(_settingsService.Ldap_BindDnFormat, username),
                        password,
                        _settingsService.Ldap_SearchBaseDn,
                        string.Format(_settingsService.Ldap_SearchFilterFormat, username),
                        _settingsService.Ldap_NameAttribute,
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
            return _settingsService.Ldap_Enable;
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
