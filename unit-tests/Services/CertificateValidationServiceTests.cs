using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using sama;
using sama.Extensions;
using sama.Models;
using sama.Services;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TestSama.Services
{
    [TestClass]
    public class CertificateValidationServiceTests
    {
        private const string EXAMPLE_CERT_PEM = @"-----BEGIN CERTIFICATE-----
MIIF8jCCBNqgAwIBAgIQDmTF+8I2reFLFyrrQceMsDANBgkqhkiG9w0BAQsFADBw
MQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3
d3cuZGlnaWNlcnQuY29tMS8wLQYDVQQDEyZEaWdpQ2VydCBTSEEyIEhpZ2ggQXNz
dXJhbmNlIFNlcnZlciBDQTAeFw0xNTExMDMwMDAwMDBaFw0xODExMjgxMjAwMDBa
MIGlMQswCQYDVQQGEwJVUzETMBEGA1UECBMKQ2FsaWZvcm5pYTEUMBIGA1UEBxML
TG9zIEFuZ2VsZXMxPDA6BgNVBAoTM0ludGVybmV0IENvcnBvcmF0aW9uIGZvciBB
c3NpZ25lZCBOYW1lcyBhbmQgTnVtYmVyczETMBEGA1UECxMKVGVjaG5vbG9neTEY
MBYGA1UEAxMPd3d3LmV4YW1wbGUub3JnMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A
MIIBCgKCAQEAs0CWL2FjPiXBl61lRfvvE0KzLJmG9LWAC3bcBjgsH6NiVVo2dt6u
Xfzi5bTm7F3K7srfUBYkLO78mraM9qizrHoIeyofrV/n+pZZJauQsPjCPxMEJnRo
D8Z4KpWKX0LyDu1SputoI4nlQ/htEhtiQnuoBfNZxF7WxcxGwEsZuS1KcXIkHl5V
RJOreKFHTaXcB1qcZ/QRaBIv0yhxvK1yBTwWddT4cli6GfHcCe3xGMaSL328Fgs3
jYrvG29PueB6VJi/tbbPu6qTfwp/H1brqdjh29U52Bhb0fJkM9DWxCP/Cattcc7a
z8EXnCO+LK8vkhw/kAiJWPKx4RBvgy73nwIDAQABo4ICUDCCAkwwHwYDVR0jBBgw
FoAUUWj/kK8CB3U8zNllZGKiErhZcjswHQYDVR0OBBYEFKZPYB4fLdHn8SOgKpUW
5Oia6m5IMIGBBgNVHREEejB4gg93d3cuZXhhbXBsZS5vcmeCC2V4YW1wbGUuY29t
ggtleGFtcGxlLmVkdYILZXhhbXBsZS5uZXSCC2V4YW1wbGUub3Jngg93d3cuZXhh
bXBsZS5jb22CD3d3dy5leGFtcGxlLmVkdYIPd3d3LmV4YW1wbGUubmV0MA4GA1Ud
DwEB/wQEAwIFoDAdBgNVHSUEFjAUBggrBgEFBQcDAQYIKwYBBQUHAwIwdQYDVR0f
BG4wbDA0oDKgMIYuaHR0cDovL2NybDMuZGlnaWNlcnQuY29tL3NoYTItaGEtc2Vy
dmVyLWc0LmNybDA0oDKgMIYuaHR0cDovL2NybDQuZGlnaWNlcnQuY29tL3NoYTIt
aGEtc2VydmVyLWc0LmNybDBMBgNVHSAERTBDMDcGCWCGSAGG/WwBATAqMCgGCCsG
AQUFBwIBFhxodHRwczovL3d3dy5kaWdpY2VydC5jb20vQ1BTMAgGBmeBDAECAjCB
gwYIKwYBBQUHAQEEdzB1MCQGCCsGAQUFBzABhhhodHRwOi8vb2NzcC5kaWdpY2Vy
dC5jb20wTQYIKwYBBQUHMAKGQWh0dHA6Ly9jYWNlcnRzLmRpZ2ljZXJ0LmNvbS9E
aWdpQ2VydFNIQTJIaWdoQXNzdXJhbmNlU2VydmVyQ0EuY3J0MAwGA1UdEwEB/wQC
MAAwDQYJKoZIhvcNAQELBQADggEBAISomhGn2L0LJn5SJHuyVZ3qMIlRCIdvqe0Q
6ls+C8ctRwRO3UU3x8q8OH+2ahxlQmpzdC5al4XQzJLiLjiJ2Q1p+hub8MFiMmVP
PZjb2tZm2ipWVuMRM+zgpRVM6nVJ9F3vFfUSHOb4/JsEIUvPY+d8/Krc+kPQwLvy
ieqRbcuFjmqfyPmUv1U9QoI4TQikpw7TZU0zYZANP4C/gj4Ry48/znmUaRvy2kvI
l7gRQ21qJTK5suoiYoYNo3J9T+pXPGU7Lydz/HwW+w0DpArtAaukI8aNX4ohFUKS
wDSiIIWIWJiJGbEeIO0TIFwEVWTOnbNl/faPXpk5IRXicapqiII=
-----END CERTIFICATE-----";
        private static X509Certificate2 EXAMPLE_CERT = new X509Certificate2(Convert.FromBase64String(@"
MIIF8jCCBNqgAwIBAgIQDmTF+8I2reFLFyrrQceMsDANBgkqhkiG9w0BAQsFADBw
MQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3
d3cuZGlnaWNlcnQuY29tMS8wLQYDVQQDEyZEaWdpQ2VydCBTSEEyIEhpZ2ggQXNz
dXJhbmNlIFNlcnZlciBDQTAeFw0xNTExMDMwMDAwMDBaFw0xODExMjgxMjAwMDBa
MIGlMQswCQYDVQQGEwJVUzETMBEGA1UECBMKQ2FsaWZvcm5pYTEUMBIGA1UEBxML
TG9zIEFuZ2VsZXMxPDA6BgNVBAoTM0ludGVybmV0IENvcnBvcmF0aW9uIGZvciBB
c3NpZ25lZCBOYW1lcyBhbmQgTnVtYmVyczETMBEGA1UECxMKVGVjaG5vbG9neTEY
MBYGA1UEAxMPd3d3LmV4YW1wbGUub3JnMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A
MIIBCgKCAQEAs0CWL2FjPiXBl61lRfvvE0KzLJmG9LWAC3bcBjgsH6NiVVo2dt6u
Xfzi5bTm7F3K7srfUBYkLO78mraM9qizrHoIeyofrV/n+pZZJauQsPjCPxMEJnRo
D8Z4KpWKX0LyDu1SputoI4nlQ/htEhtiQnuoBfNZxF7WxcxGwEsZuS1KcXIkHl5V
RJOreKFHTaXcB1qcZ/QRaBIv0yhxvK1yBTwWddT4cli6GfHcCe3xGMaSL328Fgs3
jYrvG29PueB6VJi/tbbPu6qTfwp/H1brqdjh29U52Bhb0fJkM9DWxCP/Cattcc7a
z8EXnCO+LK8vkhw/kAiJWPKx4RBvgy73nwIDAQABo4ICUDCCAkwwHwYDVR0jBBgw
FoAUUWj/kK8CB3U8zNllZGKiErhZcjswHQYDVR0OBBYEFKZPYB4fLdHn8SOgKpUW
5Oia6m5IMIGBBgNVHREEejB4gg93d3cuZXhhbXBsZS5vcmeCC2V4YW1wbGUuY29t
ggtleGFtcGxlLmVkdYILZXhhbXBsZS5uZXSCC2V4YW1wbGUub3Jngg93d3cuZXhh
bXBsZS5jb22CD3d3dy5leGFtcGxlLmVkdYIPd3d3LmV4YW1wbGUubmV0MA4GA1Ud
DwEB/wQEAwIFoDAdBgNVHSUEFjAUBggrBgEFBQcDAQYIKwYBBQUHAwIwdQYDVR0f
BG4wbDA0oDKgMIYuaHR0cDovL2NybDMuZGlnaWNlcnQuY29tL3NoYTItaGEtc2Vy
dmVyLWc0LmNybDA0oDKgMIYuaHR0cDovL2NybDQuZGlnaWNlcnQuY29tL3NoYTIt
aGEtc2VydmVyLWc0LmNybDBMBgNVHSAERTBDMDcGCWCGSAGG/WwBATAqMCgGCCsG
AQUFBwIBFhxodHRwczovL3d3dy5kaWdpY2VydC5jb20vQ1BTMAgGBmeBDAECAjCB
gwYIKwYBBQUHAQEEdzB1MCQGCCsGAQUFBzABhhhodHRwOi8vb2NzcC5kaWdpY2Vy
dC5jb20wTQYIKwYBBQUHMAKGQWh0dHA6Ly9jYWNlcnRzLmRpZ2ljZXJ0LmNvbS9E
aWdpQ2VydFNIQTJIaWdoQXNzdXJhbmNlU2VydmVyQ0EuY3J0MAwGA1UdEwEB/wQC
MAAwDQYJKoZIhvcNAQELBQADggEBAISomhGn2L0LJn5SJHuyVZ3qMIlRCIdvqe0Q
6ls+C8ctRwRO3UU3x8q8OH+2ahxlQmpzdC5al4XQzJLiLjiJ2Q1p+hub8MFiMmVP
PZjb2tZm2ipWVuMRM+zgpRVM6nVJ9F3vFfUSHOb4/JsEIUvPY+d8/Krc+kPQwLvy
ieqRbcuFjmqfyPmUv1U9QoI4TQikpw7TZU0zYZANP4C/gj4Ry48/znmUaRvy2kvI
l7gRQ21qJTK5suoiYoYNo3J9T+pXPGU7Lydz/HwW+w0DpArtAaukI8aNX4ohFUKS
wDSiIIWIWJiJGbEeIO0TIFwEVWTOnbNl/faPXpk5IRXicapqiII="));

        private SettingsService _settingsService;
        private CertificateValidationService _service;

        [TestInitialize]
        public void Setup()
        {
            _settingsService = Substitute.For<SettingsService>((IServiceProvider)null);

            _service = new CertificateValidationService(_settingsService);
        }

        [TestMethod]
        public void ValidateLdapShouldReturnWhenIgnoreOptionIsSet()
        {
            _settingsService.Ldap_SslIgnoreValidity.Returns(true);

            // Should not throw:
            _service.ValidateLdap(null, System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch);
        }

        [TestMethod]
        public void ValidateLdapShouldThrowWhenUsingCustomCertAndNonChainPolicyErrorsExist()
        {
            _settingsService.Ldap_SslIgnoreValidity.Returns(false);
            _settingsService.Ldap_SslValidCert.Returns(EXAMPLE_CERT_PEM);

            AssertSslException(() => _service.ValidateLdap(null, System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch), true, "certificate name is mismatched");
        }

        [TestMethod]
        public void ValidateLdapShouldThrowWhenUsingCustomCertAndUnacceptableChainErrorsExist()
        {
            _settingsService.Ldap_SslIgnoreValidity.Returns(false);
            _settingsService.Ldap_SslValidCert.Returns(EXAMPLE_CERT_PEM);

            var chain = new X509Chain();
            chain.ChainPolicy.VerificationTime = new DateTime(2019, 1, 1);
            chain.Build(EXAMPLE_CERT);

            AssertSslException(() => _service.ValidateLdap(chain, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors), true, "certificate is not within its validity period");
        }

        [TestMethod]
        public void ValidateLdapShouldReturnWhenUsingCustomCertWithoutUnacceptableChainErrors()
        {
            _settingsService.Ldap_SslIgnoreValidity.Returns(false);
            _settingsService.Ldap_SslValidCert.Returns(EXAMPLE_CERT_PEM);

            var chain = new X509Chain();
            chain.ChainPolicy.VerificationTime = new DateTime(2018, 1, 1);
            chain.Build(EXAMPLE_CERT);

            // Should not throw:
            _service.ValidateLdap(chain, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors);
        }

        [TestMethod]
        public void ValidateHttpEndpointShouldReturnWhenIgnoreOptionIsSet()
        {
            var ep = CreateTestEndpoint(true, null);

            // Should not throw:
            _service.ValidateHttpEndpoint(ep, null, System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch);
        }

        [TestMethod]
        public void ValidateHttpEndpointShouldThrowWhenUsingCustomCertAndNonChainPolicyErrorsExist()
        {
            var ep = CreateTestEndpoint(false, EXAMPLE_CERT_PEM);

            AssertSslException(() => _service.ValidateHttpEndpoint(ep, null, System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch), false, "certificate name is mismatched");
        }

        [TestMethod]
        public void ValidateHttpEndpointShouldThrowWhenUsingCustomCertAndUnacceptableChainErrorsExist()
        {
            var ep = CreateTestEndpoint(false, EXAMPLE_CERT_PEM);

            var chain = new X509Chain();
            chain.ChainPolicy.VerificationTime = new DateTime(2019, 1, 1);
            chain.Build(EXAMPLE_CERT);

            AssertSslException(() => _service.ValidateHttpEndpoint(ep, chain, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors), false, "certificate is not within its validity period");
        }

        [TestMethod]
        public void ValidateHttpEndpointShouldReturnWhenUsingCustomCertWithoutUnacceptableChainErrors()
        {
            var ep = CreateTestEndpoint(false, EXAMPLE_CERT_PEM);

            var chain = new X509Chain();
            chain.ChainPolicy.VerificationTime = new DateTime(2018, 1, 1);
            chain.Build(EXAMPLE_CERT);

            // Should not throw:
            _service.ValidateHttpEndpoint(ep, chain, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors);
        }

        private Endpoint CreateTestEndpoint(bool ignoreCerts, string customCert)
        {
            var ep = new Endpoint { Name = "test1", Kind = Endpoint.EndpointKind.Http };
            ep.SetHttpIgnoreTlsCerts(ignoreCerts);
            ep.SetHttpCustomTlsCert(customCert);
            return ep;
        }

        private void AssertSslException(Action action, bool ldap, string partialDetailsText)
        {
            try
            {
                action.Invoke();
                Assert.Fail("Action did not throw the expected SslException");
            }
            catch (SslException sslEx)
            {
                StringAssert.Contains(sslEx.Message, ldap ? "LDAP" : "HTTPS");
                StringAssert.Contains(sslEx.Details, partialDetailsText);
            }
        }
    }
}
