using sama.Extensions;
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
                if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                {
                    // Unacceptable errors are present; throw
                    throw GetAppropriateException(true, chain, sslPolicyErrors, false);
                }
                foreach (var el in chain.ChainElements)
                {
                    if (el.ChainElementStatus != null && el.ChainElementStatus.Length > 0)
                    {
                        foreach (var status in el.ChainElementStatus)
                        {
                            if (status.Status == X509ChainStatusFlags.NoError) continue;
                            if (status.Status.HasFlag(X509ChainStatusFlags.Cyclic) || status.Status.HasFlag(X509ChainStatusFlags.ExplicitDistrust) || status.Status.HasFlag(X509ChainStatusFlags.NotSignatureValid) || status.Status.HasFlag(X509ChainStatusFlags.Revoked))
                            {
                                // Unacceptable errors are present; throw
                                throw GetAppropriateException(true, chain, sslPolicyErrors, false);
                            }
                        }
                    }
                }

                // Everything looks good so far; load the custom cert
                var cert = LoadCert(true, _settingsService.Ldap_SslValidCert);

                foreach (var el in chain.ChainElements)
                {
                    if (el.Certificate.Thumbprint.Equals(cert.Thumbprint))
                    {
                        // Got a matching cert; everything is okay
                        return;
                    }
                }

                throw GetAppropriateException(true, chain, sslPolicyErrors, true);
            }
            else
            {
                // Use existing status information
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return; // OK
                }
                throw GetAppropriateException(true, chain, sslPolicyErrors, false);
            }
        }

        public virtual void ValidateHttpEndpoint(Models.Endpoint endpoint, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (endpoint.GetHttpIgnoreTlsCerts())
            {
                return; // Everything is OK
            }

            var customCertPem = endpoint.GetHttpCustomTlsCert();
            if (!string.IsNullOrWhiteSpace(customCertPem))
            {
                if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                {
                    // Unacceptable errors are present; throw
                    throw GetAppropriateException(false, chain, sslPolicyErrors, false);
                }
                foreach (var el in chain.ChainElements)
                {
                    if (el.ChainElementStatus != null && el.ChainElementStatus.Length > 0)
                    {
                        foreach (var status in el.ChainElementStatus)
                        {
                            if (status.Status == X509ChainStatusFlags.NoError) continue;
                            if (status.Status.HasFlag(X509ChainStatusFlags.Cyclic) || status.Status.HasFlag(X509ChainStatusFlags.ExplicitDistrust) || status.Status.HasFlag(X509ChainStatusFlags.NotSignatureValid) || status.Status.HasFlag(X509ChainStatusFlags.Revoked))
                            {
                                // Unacceptable errors are present; throw
                                throw GetAppropriateException(false, chain, sslPolicyErrors, false);
                            }
                        }
                    }
                }

                // Everything looks good so far; load the custom cert
                var cert = LoadCert(false, customCertPem);

                foreach (var el in chain.ChainElements)
                {
                    if (el.Certificate.Thumbprint.Equals(cert.Thumbprint))
                    {
                        // Got a matching cert; everything is okay
                        return;
                    }
                }

                throw GetAppropriateException(false, chain, sslPolicyErrors, true);
            }
            else
            {
                // Use existing status information
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return; // OK
                }
                throw GetAppropriateException(false, chain, sslPolicyErrors, false);
            }
        }

        private X509Certificate2 LoadCert(bool ldap, string pemCert)
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
                throw SslException.CreateException(ldap, $"The custom certificate could not be loaded: {ex.Message}");
            }
        }

        private Exception GetAppropriateException(bool ldap, X509Chain chain, SslPolicyErrors sslPolicyErrors, bool customCertMismatch)
        {
            var stb = new StringBuilder();
            if (customCertMismatch)
            {
                stb.Append("\r\n✯ The certificate chain does not contain the custom certificate:");
                foreach (var el in chain.ChainElements)
                {
                    stb.Append($"\r\n  → Subject=\"{el.Certificate.Subject}\" (Thumbprint={el.Certificate.Thumbprint}) ");
                }
            }
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
            {
                stb.Append("\r\n✯ The certificate name is mismatched");
            }
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
            {
                stb.Append("\r\n✯ The certificate is unavailable");
            }
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) && !customCertMismatch)
            {
                stb.Append("\r\n✯ The certificate chain has errors:");
                foreach (var el in chain.ChainElements)
                {
                    stb.Append($"\r\n  → Subject=\"{el.Certificate.Subject}\" (Thumbprint={el.Certificate.Thumbprint}) ");
                    if (el.ChainElementStatus == null || el.ChainElementStatus.Length == 0)
                    {
                        stb.Append("[OK]");
                    }
                    else
                    {
                        foreach (var status in el.ChainElementStatus)
                        {
                            stb.Append($"[{status.StatusInformation.Trim()}]");
                        }
                    }
                }
            }

            return SslException.CreateException(ldap, stb.ToString().Trim());
        }
    }
}
