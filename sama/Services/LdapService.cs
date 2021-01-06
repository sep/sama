using sama.Models;
using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace sama.Services
{
    public class LdapService
    {
        private readonly SettingsService _settingsService;
        private readonly CertificateValidationService _certificateValidationService;
        private readonly LdapAuthWrapper _ldapWrapper;

        public LdapService(SettingsService settingsService, CertificateValidationService certificateValidationService, LdapAuthWrapper ldapWrapper)
        {
            _settingsService = settingsService;
            _certificateValidationService = certificateValidationService;
            _ldapWrapper = ldapWrapper;
        }

        public virtual ApplicationUser Authenticate(string username, string password)
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

            var userId = Guid.NewGuid().ToByteArray();
            userId[0] = 0;
            userId[1] = 0;
            userId[2] = 0;
            userId[3] = 0;
            return new ApplicationUser
            {
                Id = new Guid(userId),
                UserName = ldapUser.DisplayName,
                IsRemote = true
            };
        }

        public virtual bool IsLdapEnabled()
        {
            return _settingsService.Ldap_Enable;
        }

        private bool Ldap_UserDefinedServerCertValidationDelegate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            _certificateValidationService.ValidateLdap(chain, sslPolicyErrors); // throws on error
            return true;
        }
    }
}
