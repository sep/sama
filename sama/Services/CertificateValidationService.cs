using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace sama.Services
{
    public class CertificateValidationService
    {
        private readonly SettingsService _settingsService;

        public CertificateValidationService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public virtual void ValidateLdap(X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_settingsService.Ldap_SslIgnoreValidity)
            {
                return; // Everything is OK
            }

            if (!string.IsNullOrWhiteSpace(_settingsService.Ldap_SslValidCert))
            {
                if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch) || sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                {
                    // Unacceptable errors are present; throw
                    throw GetAppropriateException(chain, sslPolicyErrors, false);
                }
                foreach (var el in chain.ChainElements)
                {
                    if (el.ChainElementStatus != null && el.ChainElementStatus.Length > 0)
                    {
                        foreach (var status in el.ChainElementStatus)
                        {
                            if (status.Status != X509ChainStatusFlags.NoError && status.Status != X509ChainStatusFlags.UntrustedRoot)
                            {
                                // Unacceptable errors are present; throw
                                throw GetAppropriateException(chain, sslPolicyErrors, false);
                            }
                        }
                    }
                }

                // Everything looks good so far; load the custom cert
                var cert = LoadCert(_settingsService.Ldap_SslValidCert);

                foreach (var el in chain.ChainElements)
                {
                    if (el.Certificate.Thumbprint.Equals(cert.Thumbprint))
                    {
                        // Got a matching cert; everything is okay
                        return;
                    }
                }

                throw GetAppropriateException(chain, sslPolicyErrors, true);
            }
            else
            {
                // Use existing status information
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return; // OK
                }
                throw GetAppropriateException(chain, sslPolicyErrors, false);
            }
        }

        private X509Certificate2 LoadCert(string pemCert)
        {
            try
            {
                const string certStart = "-----BEGIN CERTIFICATE-----";
                const string certEnd = "-----END CERTIFICATE-----";

                var start = pemCert.IndexOf(certStart) + certStart.Length;
                var end = pemCert.IndexOf(certEnd, start);

                var certData = Convert.FromBase64String(pemCert.Substring(start, end - start));

                return new X509Certificate2(certData);
            }
            catch (Exception ex)
            {
                throw new SslException($"The custom certificate could not be loaded: {ex.Message}");
            }
        }

        private Exception GetAppropriateException(X509Chain chain, SslPolicyErrors sslPolicyErrors, bool customCertMismatch)
        {
            var stb = new StringBuilder();
            if (customCertMismatch)
            {
                stb.Append(" ✯ The certificate chain does not contain the custom certificate:");
                foreach (var el in chain.ChainElements)
                {
                    stb.Append($" → Subject=\"{el.Certificate.Subject}\" (Thumbprint={el.Certificate.Thumbprint}) ");
                }
            }
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                stb.Append(" ✯ The certificate name is mismatched");
            }
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
            {
                stb.Append(" ✯ The certificate is unavailable");
            }
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) && !customCertMismatch)
            {
                stb.Append(" ✯ The certificate chain has errors:");
                foreach (var el in chain.ChainElements)
                {
                    stb.Append($" → Subject=\"{el.Certificate.Subject}\" (Thumbprint={el.Certificate.Thumbprint}) ");
                    if (el.ChainElementStatus == null || el.ChainElementStatus.Length == 0)
                    {
                        stb.Append("[OK]");
                    }
                    else
                    {
                        foreach (var status in el.ChainElementStatus)
                        {
                            stb.Append($"[{status.StatusInformation}]");
                        }
                    }
                }
            }

            return new SslException(stb.ToString());
        }
    }
}
